using DM_wraper_NS;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DM_process_lib
{
    internal class MainProces : IDM_process
    {
        private static int _usedDataIndex = 0;
        private static readonly object _lockObject = new object();
        private static readonly Random _random = new Random();
        private const double ERROR_PERCENTAGE = 0.05; // 5% error rate
        private const double DUPLICATE_PERCENTAGE = 0.15; // 15% chance of duplicate
        private static List<int> _lastScanRecordIndices = new List<int>(); // Track which records were used in last scan
        private static bool _hasErrorsInLastScan = false; // Track if last scan had any errors

        // Добавляем хранилище для отслеживания уже использованных DataMatrix кодов
        private static readonly List<string> _usedDataMatrixCodes = new List<string>();
        private static readonly int MAX_STORED_CODES = 100; // Максимальное количество хранимых кодов для дубликатов

        public MainProces()
        {
        }

        public void MP_TakeShot()
        {
            Thread th_pShot = new Thread(_shot_proces);
            th_pShot.IsBackground = true;
            th_pShot.Start();
        }

        public void MP_StopShot()
        {
            lock (_lockObject)
            {
                _usedDataIndex = 0; // Reset to start from beginning
                _lastScanRecordIndices.Clear(); // Clear last scan tracking
                _hasErrorsInLastScan = false; // Reset error tracking
                // Очищаем историю DataMatrix кодов при остановке
                _usedDataMatrixCodes.Clear();
            }
        }

        private void _shot_proces()
        {
            Console.WriteLine("MainProces : Take a shot");

            Dictionary<string, bool> dataModel = ExtractDataModelFromFRX();
            List<BOX_data> lDMD = new List<BOX_data>();

            // Load ARM_JOB_SGTIN data
            string baseDir = Path.GetDirectoryName(typeof(MainProces).Assembly.Location)!;
            string armJobPath = Path.Combine(baseDir, "ARM_JOB_SGTIN.json");

            if (!File.Exists(armJobPath))
            {
                Console.WriteLine("ARM_JOB_SGTIN.json file not found!");
                return;
            }

            string armJobData = File.ReadAllText(armJobPath);
            ArmJobData armData = JsonConvert.DeserializeObject<ArmJobData>(armJobData);

            // Determine scan mode and record indices to use
            List<int> recordIndicesToUse = new List<int>();
            bool isRescanMode = false;

            lock (_lockObject)
            {
                if (_hasErrorsInLastScan && _lastScanRecordIndices.Count > 0)
                {
                    // Rescan mode: use the same records as last scan, but without errors
                    // НЕ сдвигаем _usedDataIndex - остаемся на тех же записях
                    recordIndicesToUse = new List<int>(_lastScanRecordIndices);
                    isRescanMode = true;
                    _hasErrorsInLastScan = false; // Reset for next iteration
                    Console.WriteLine($"RESCAN MODE: Re-processing {recordIndicesToUse.Count} records from indices {recordIndicesToUse[0] + 1} to {recordIndicesToUse[recordIndicesToUse.Count - 1] + 1} WITHOUT errors/duplicates");
                }
                else
                {
                    // Normal mode: get next 20 records
                    // Только здесь сдвигаем _usedDataIndex если предыдущее сканирование было успешным
                    for (int i = 0; i < 20; i++)
                    {
                        if (_usedDataIndex >= armData.RECORDSET.Count)
                        {
                            _usedDataIndex = 0; // Cycle back to beginning
                        }
                        recordIndicesToUse.Add(_usedDataIndex);
                        _usedDataIndex++;
                    }
                    _lastScanRecordIndices = new List<int>(recordIndicesToUse);
                    Console.WriteLine($"NORMAL MODE: Processing NEW records {recordIndicesToUse[0] + 1} to {recordIndicesToUse[recordIndicesToUse.Count - 1] + 1}");
                }
            }

            // Generate cells with the specified positioning pattern
            int basePoseX = 518;
            int basePoseY = 649;
            int cellWidth = 326;
            int cellHeight = 326;
            int xOffset = 380;
            int yOffset = 385;
            int cellsPerRow = 12;

            bool currentScanHasErrors = false;

            for (int cellIndex = 0; cellIndex < recordIndicesToUse.Count; cellIndex++)
            {
                int row = cellIndex / cellsPerRow;
                int col = cellIndex % cellsPerRow;

                int currentPoseX = basePoseX + (col * xOffset);
                int currentPoseY = basePoseY + (row * yOffset);

                // Get ARM record for this cell
                int recordIndex = recordIndicesToUse[cellIndex];
                ArmRecord currentRecord = armData.RECORDSET[recordIndex];

                // Generate DataMatrix data (with possible duplicate)
                bool cellHasDuplicate = false;
                string dataMatrixData = GenerateDataMatrixData(currentRecord, cellIndex + 1, isRescanMode, ref cellHasDuplicate);

                // Apply or skip errors based on mode
                string gtinData = "04603905002474";
                string serialNumberData = currentRecord.UN_CODE;
                string seriesNameData = "TEST30Х30";
                string expireDateData = "06 28";

                if (!isRescanMode)
                {
                    // Normal mode: apply random errors with 5% chance
                    bool cellHasError = false;

                    dataMatrixData = ApplyRandomError(dataMatrixData, cellIndex + 1, "DataMatrix", ref cellHasError);
                    gtinData = ApplyRandomError(gtinData, cellIndex + 1, "GTIN", ref cellHasError);
                    serialNumberData = ApplyRandomError(serialNumberData, cellIndex + 1, "SerialNumber", ref cellHasError);
                    seriesNameData = ApplyRandomError(seriesNameData, cellIndex + 1, "SeriesName", ref cellHasError);
                    expireDateData = ApplyRandomError(expireDateData, cellIndex + 1, "ExpireDate", ref cellHasError);

                    // Дубликат тоже считается ошибкой
                    if (cellHasError || cellHasDuplicate)
                    {
                        currentScanHasErrors = true;
                    }
                }
                else
                {
                    // Rescan mode: use clean data (no errors and no duplicates)
                    Console.WriteLine($"Cell {cellIndex + 1}: Clean rescan of record {recordIndex + 1} (UN_CODE: {currentRecord.UN_CODE})");
                }

                // Create cell with data
                Cell templateCell = CreateCellWithData(cellIndex + 1, currentPoseX, currentPoseY, cellWidth, cellHeight,
                    dataMatrixData, gtinData, serialNumberData, seriesNameData, expireDateData);

                BOX_data dataCell = CreateDataCellFromModel(dataModel, templateCell);
                lDMD.Add(dataCell);
            }

            // Update error tracking for next scan
            lock (_lockObject)
            {
                if (currentScanHasErrors)
                {
                    // При ошибках НЕ обновляем _lastScanRecordIndices и НЕ сдвигаем _usedDataIndex
                    // Они уже содержат правильные индексы для повторного сканирования
                    _hasErrorsInLastScan = true;

                    // Откатываем _usedDataIndex обратно, чтобы в следующий раз начать с тех же записей
                    _usedDataIndex -= recordIndicesToUse.Count;
                    if (_usedDataIndex < 0)
                    {
                        _usedDataIndex = armData.RECORDSET.Count + _usedDataIndex; // Обработка отрицательного значения
                    }

                    Console.WriteLine($"Errors or duplicates detected - _usedDataIndex rolled back to {_usedDataIndex}. Next scan will be a RESCAN of the same records");
                }
                else
                {
                    _hasErrorsInLastScan = false;
                    if (isRescanMode)
                    {
                        Console.WriteLine("Rescan completed successfully - next scan will be NORMAL mode with new records");
                    }
                    else
                    {
                        Console.WriteLine("Normal scan completed successfully - next scan will process new records");
                    }
                }
            }

            result_data dmrd = new result_data();
            dmrd.BOXs = lDMD;
            string imagePath = Path.Combine(baseDir, "image_raw.jpg");
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                dmrd.rawImage = Image.Load<Rgba32>(ms);
            }

            string modeText = isRescanMode ? "RESCAN" : "NORMAL";
            Console.WriteLine($"Add new DM codes - {modeText} mode, processed cells: {lDMD.Count}");
            _DMP._dM_recogn_wraper.Update_result_data(dmrd);
        }

        /// <summary>
        /// Генерирует DataMatrix код с возможностью создания дубликата
        /// </summary>
        private string GenerateDataMatrixData(ArmRecord currentRecord, int cellIndex, bool isRescanMode, ref bool hasDuplicate)
        {
            lock (_lockObject)
            {
                if (isRescanMode)
                {
                    // В режиме пересканирования всегда генерируем новый уникальный код
                    string newCode = $"010460390500247421{currentRecord.UN_CODE}\u001D91{currentRecord.GS1FIELD91}\u001D92{currentRecord.GS1FIELD92}";

                    // Добавляем в список использованных кодов
                    _usedDataMatrixCodes.Add(newCode);

                    // Ограничиваем размер списка для экономии памяти
                    if (_usedDataMatrixCodes.Count > MAX_STORED_CODES)
                    {
                        _usedDataMatrixCodes.RemoveAt(0); // Удаляем самый старый код
                    }

                    Console.WriteLine($"Cell {cellIndex}: RESCAN - NEW unique DataMatrix code generated: {newCode}");
                    return newCode;
                }

                // Проверяем, нужно ли создать дубликат (только в обычном режиме)
                bool shouldCreateDuplicate = _usedDataMatrixCodes.Count > 0 &&
                                           _random.NextDouble() < DUPLICATE_PERCENTAGE;

                if (shouldCreateDuplicate)
                {
                    // Выбираем случайный код из уже использованных
                    string duplicateCode = _usedDataMatrixCodes[_random.Next(_usedDataMatrixCodes.Count)];
                    hasDuplicate = true; // Помечаем что это дубликат (ошибка)
                    Console.WriteLine($"Cell {cellIndex}: DUPLICATE DataMatrix code generated: {duplicateCode}");
                    return duplicateCode;
                }
                else
                {
                    // Создаем новый код
                    string newCode = $"010460390500247421{currentRecord.UN_CODE}\u001D91{currentRecord.GS1FIELD91}\u001D92{currentRecord.GS1FIELD92}";

                    // Добавляем в список использованных кодов
                    _usedDataMatrixCodes.Add(newCode);

                    // Ограничиваем размер списка для экономии памяти
                    if (_usedDataMatrixCodes.Count > MAX_STORED_CODES)
                    {
                        _usedDataMatrixCodes.RemoveAt(0); // Удаляем самый старый код
                    }

                    Console.WriteLine($"Cell {cellIndex}: NEW DataMatrix code generated: {newCode}");
                    return newCode;
                }
            }
        }

        /// <summary>
        /// Creates a cell with specified data
        /// </summary>
        private Cell CreateCellWithData(int cellId, int poseX, int poseY, int width, int height,
            string dataMatrixData, string gtinData, string serialNumberData, string seriesNameData, string expireDateData)
        {
            return new Cell
            {
                cell_id = cellId,
                poseX = poseX,
                poseY = poseY,
                width = width,
                height = height,
                angle = 0,
                cell_dm = new DM_data
                {
                    data = dataMatrixData,
                    poseX = 170,
                    poseY = 100,
                    width = 132,
                    height = 130,
                    angle = 0,
                    isError = false
                },
                cell_ocr = new List<Cell_OCR>
                {
                    new Cell_OCR
                    {
                        data = "GTIN",
                        name = "GTIN",
                        poseX = 50,
                        poseY = 190,
                        width = 84,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = "SN",
                        name = "SN",
                        poseX = 44,
                        poseY = 226,
                        width = 72,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = "Серия",
                        name = "Серия",
                        poseX = 56,
                        poseY = 263,
                        width = 96,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = "Годен до",
                        name = "Годен до",
                        poseX = 68,
                        poseY = 299,
                        width = 121,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = gtinData,
                        name = "Gtin",
                        poseX = 219,
                        poseY = 190,
                        width = 205,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = serialNumberData,
                        name = "SerialNumber",
                        poseX = 219,
                        poseY = 226,
                        width = 205,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = seriesNameData,
                        name = "SeriesName",
                        poseX = 219,
                        poseY = 263,
                        width = 205,
                        height = 27,
                        angle = 1
                    },
                    new Cell_OCR
                    {
                        data = expireDateData,
                        name = "ExpireDate",
                        poseX = 231,
                        poseY = 299,
                        width = 181,
                        height = 27,
                        angle = 1
                    }
                }
            };
        }

        /// <summary>
        /// Gets ARM record for a specific cell index
        /// </summary>
        private ArmRecord GetArmRecordForCell(ArmJobData armData, int cellIndex)
        {
            if (armData.RECORDSET.Count == 0)
                return null;

            // Use modulo to cycle through records if we have more cells than records
            int recordIndex = cellIndex % armData.RECORDSET.Count;
            return armData.RECORDSET[recordIndex];
        }

        /// <summary>
        /// Applies random error to data with 5% probability
        /// </summary>
        private string ApplyRandomError(string originalData, int cellIndex, string dataType, ref bool hasError)
        {
            if (string.IsNullOrEmpty(originalData))
                return originalData;

            // 5% chance of error
            if (_random.NextDouble() < ERROR_PERCENTAGE)
            {
                hasError = true;
                string corrupted = CorruptData(originalData);
                Console.WriteLine($"Cell {cellIndex + 1} - {dataType} error: '{originalData}' -> '{corrupted}'");
                return corrupted;
            }

            return originalData;
        }

        /// <summary>
        /// Corrupts data by changing random characters to create invalid values
        /// </summary>
        private string CorruptData(string data)
        {
            if (string.IsNullOrEmpty(data))
                return data;

            char[] corrupted = data.ToCharArray();

            // Change 1-3 random characters
            int numChanges = _random.Next(1, Math.Min(4, corrupted.Length + 1));

            for (int i = 0; i < numChanges; i++)
            {
                int index = _random.Next(corrupted.Length);
                char originalChar = corrupted[index];

                // Generate a different character
                char newChar;
                do
                {
                    if (char.IsDigit(originalChar))
                    {
                        // For digits, change to another digit or letter
                        newChar = _random.Next(2) == 0 ?
                            (char)(_random.Next(10) + '0') :
                            (char)(_random.Next(26) + 'A');
                    }
                    else if (char.IsLetter(originalChar))
                    {
                        // For letters, change to another letter or digit
                        newChar = _random.Next(2) == 0 ?
                            (char)(_random.Next(26) + 'A') :
                            (char)(_random.Next(10) + '0');
                    }
                    else
                    {
                        // For other characters, change to random alphanumeric
                        newChar = _random.Next(2) == 0 ?
                            (char)(_random.Next(26) + 'A') :
                            (char)(_random.Next(10) + '0');
                    }
                } while (newChar == originalChar);

                corrupted[index] = newChar;
            }

            string result = new string(corrupted);
            return result;
        }

        /// <summary>
        /// Gets the next available ARM record and increments the index (legacy method for compatibility)
        /// </summary>
        private ArmRecord GetNextArmRecord(ArmJobData armData)
        {
            lock (_lockObject)
            {
                if (_usedDataIndex >= armData.RECORDSET.Count)
                {
                    // If we've used all records, start over
                    _usedDataIndex = 0;
                }

                if (armData.RECORDSET.Count == 0)
                {
                    return null;
                }

                ArmRecord record = armData.RECORDSET[_usedDataIndex];
                _usedDataIndex++;

                Console.WriteLine($"Using ARM record {_usedDataIndex}/{armData.RECORDSET.Count}: UN_CODE={record.UN_CODE}");
                return record;
            }
        }

        /// <summary>
        /// Отдельная функция, которая формирует BOX_data, заполняет его списком OCR 
        /// на основе dataModel и массива predefinedData
        /// </summary>
        private BOX_data CreateDataCellFromModel(Dictionary<string, bool> dataModel, Cell cell)
        {
            BOX_data dataCell = new BOX_data
            {
                poseX = cell.poseX,
                poseY = cell.poseY,
                width = cell.width,
                height = cell.height,
                alpha = cell.angle,
                packType = "PackageType",
                isError = false,
                OCR = new List<OCR_data>(),
                DM = new DM_wraper_NS.DM_data
                {
                    data = cell.cell_dm?.data,
                    poseX = cell.cell_dm?.poseX ?? 0,
                    poseY = cell.cell_dm?.poseY ?? 0,
                    width = cell.cell_dm?.width ?? 0,
                    height = cell.cell_dm?.height ?? 0,
                    alpha = cell.cell_dm?.angle ?? 0,
                    isError = cell.cell_dm?.isError ?? false
                }
            };

            foreach (Cell_OCR entry in cell.cell_ocr)
            {
                dataCell.OCR.Add(new OCR_data
                {
                    Text = entry.data,
                    Name = entry.name,
                    poseX = entry.poseX,
                    poseY = entry.poseY,
                    width = entry.width,
                    height = entry.height,
                    alpha = entry.angle,
                });
            }
            return dataCell;
        }

        public XDocument OriginalDocument { get; private set; }

        private Dictionary<string, bool> ExtractDataModelFromFRX()
        {
            Dictionary<string, bool> dataModel = new Dictionary<string, bool>();
            string base64Frx = _DMP._pathToPrintPattern;

            if (string.IsNullOrEmpty(base64Frx))
            {
                Console.WriteLine("Base64-строка для FRX не задана, создаём пустую модель.");
                dataModel["Default"] = false;
                return dataModel;
            }

            try
            {
                string xmlString;

                if (IsBase64String(base64Frx))
                {
                    byte[] frxBytes = Convert.FromBase64String(base64Frx);
                    xmlString = Encoding.UTF8.GetString(frxBytes);
                }
                else
                {
                    xmlString = base64Frx;
                }

                using (var reader = new StringReader(xmlString))
                {
                    OriginalDocument = XDocument.Load(reader);
                }

                dataModel = ExtractFieldsFromReport(OriginalDocument, "LabelQry");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки FastReport: {ex.Message}");
            }

            return dataModel;
        }

        private bool IsBase64String(string s)
        {
            s = s.Trim();
            return (s.Length % 4 == 0) &&
                   System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,2}$");
        }

        private static Dictionary<string, bool> ExtractFieldsFromReport(XDocument reportDocument, string prefix)
        {
            var resultFields = new Dictionary<string, bool>();

            // Найдём все элементы, у которых Type = "variable" и задан Name
            var variableElements = reportDocument
                .Descendants()
                .Where(el =>
                    el.Attribute("Type")?.Value == "variable" &&
                    !string.IsNullOrWhiteSpace(el.Attribute("Name")?.Value)
                );

            foreach (var element in variableElements)
            {
                var name = element.Attribute("Name")?.Value?.Trim();
                if (!string.IsNullOrEmpty(name) && !resultFields.ContainsKey(name))
                {
                    resultFields[name] = true;
                }
            }

            return resultFields;
        }

        public void MP_StartShot()
        {
            _DMP.update_PP();
        }

        private class ResultData
        {
            public List<Cell> cells { get; set; }
        }

        private class ArmJobData
        {
            public List<ArmRecord> RECORDSET { get; set; }
        }

        private class ArmRecord
        {
            public string UNID { get; set; }
            public string UN_BARCODE { get; set; }
            public string UN_CODE { get; set; }
            public string GS1FIELD91 { get; set; }
            public string GS1FIELD92 { get; set; }
            public string GS1FIELD93 { get; set; }
            public string UN_CODE_STATEID { get; set; }
            public string UN_CODE_STATE { get; set; }
        }

        private class Cell
        {
            public int cell_id { get; set; }
            public int poseX { get; set; }
            public int poseY { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int angle { get; set; }
            public DM_data cell_dm { get; set; }
            public List<Cell_OCR> cell_ocr { get; set; }
        }

        public class DM_data
        {
            public string data { get; set; }
            public int poseX { get; set; }
            public int poseY { get; set; }
            public int height { get; set; }
            public int width { get; set; }
            public int angle { get; set; }
            public bool isError { get; set; }
        }

        private class Cell_OCR
        {
            public string data { get; set; }
            public string name { get; set; }
            public int poseX { get; set; }
            public int poseY { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int angle { get; set; }
        }
    }
}