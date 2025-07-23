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
        private static int _currentBatchStartIndex = 0; // Индекс начала текущей партии (0, 84, 168, ...)
        private static readonly object _lockObject = new object();
        private static readonly Random _random = new Random();
        private const double ERROR_PERCENTAGE = 0.05; // 5% error rate
        private const double DUPLICATE_PERCENTAGE = 0.15; // 15% chance of duplicate
        private const int BATCH_SIZE = 84; // Размер партии для сканирования
        private static bool _lastScanHadErrors = false; // Был ли последний скан с ошибками
        private static List<int> _currentBatchIndices = new List<int>(); // Индексы текущей партии

        // Добавляем хранилище для отслеживания уже использованных DataMatrix кодов
        private static readonly List<string> _usedDataMatrixCodes = new List<string>();
        private static readonly int MAX_STORED_CODES = 500; // Максимальное количество хранимых кодов для дубликатов

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
                _currentBatchStartIndex = 0; // Reset to start from beginning
                _lastScanHadErrors = false; // Reset error tracking
                _currentBatchIndices.Clear(); // Clear current batch tracking
                // Очищаем историю DataMatrix кодов при остановке
                _usedDataMatrixCodes.Clear();
            }
        }

        //private void _shot_proces()
        //{
        //    Console.WriteLine("MainProces : Take a shot");

        //    Dictionary<string, bool> dataModel = ExtractDataModelFromFRX();
        //    List<BOX_data> lDMD = new List<BOX_data>();

        //    // Load ARM_JOB_SGTIN data
        //    string baseDir = Path.GetDirectoryName(typeof(MainProces).Assembly.Location)!;
        //    string armJobPath = Path.Combine(baseDir, "ARM_JOB_SGTIN.json");

        //    if (!File.Exists(armJobPath))
        //    {
        //        Console.WriteLine("ARM_JOB_SGTIN.json file not found!");
        //        return;
        //    }

        //    string armJobData = File.ReadAllText(armJobPath);
        //    ArmJobData armData = JsonConvert.DeserializeObject<ArmJobData>(armJobData);

        //    // Determine scan mode and record indices to use
        //    List<int> recordIndicesToUse = new List<int>();
        //    bool isRescanMode = false;

        //    lock (_lockObject)
        //    {
        //        if (_lastScanHadErrors)
        //        {
        //            // Режим пересканирования: используем те же записи, что и в предыдущем скане
        //            recordIndicesToUse = new List<int>(_currentBatchIndices);
        //            isRescanMode = true;
        //            _lastScanHadErrors = false; // Сбрасываем флаг ошибок для следующей итерации
        //            Console.WriteLine($"RESCAN MODE: Re-processing {recordIndicesToUse.Count} records from indices {recordIndicesToUse[0]} to {recordIndicesToUse[recordIndicesToUse.Count - 1]} WITHOUT errors/duplicates");
        //        }
        //        else
        //        {
        //            // Обычный режим: берем следующие 84 записи
        //            _currentBatchIndices.Clear();
        //            for (int i = 0; i < BATCH_SIZE; i++)
        //            {
        //                int recordIndex = (_currentBatchStartIndex + i) % armData.RECORDSET.Count;
        //                _currentBatchIndices.Add(recordIndex);
        //            }
        //            recordIndicesToUse = new List<int>(_currentBatchIndices);

        //            Console.WriteLine($"NORMAL MODE: Processing records from indices {_currentBatchStartIndex} to {(_currentBatchStartIndex + BATCH_SIZE - 1) % armData.RECORDSET.Count} (batch start: {_currentBatchStartIndex})");
        //        }
        //    }

        //    // Generate cells with the specified positioning pattern
        //    int basePoseX = 518;
        //    int basePoseY = 649;
        //    int cellWidth = 326;
        //    int cellHeight = 326;
        //    int xOffset = 380;
        //    int yOffset = 385;
        //    int cellsPerRow = 12;

        //    bool currentScanHasErrors = false;

        //    // Цикл всегда от 0 до 83 (cellIndex), но recordIndex берется из recordIndicesToUse
        //    for (int cellIndex = 0; cellIndex < BATCH_SIZE; cellIndex++)
        //    {
        //        int row = cellIndex / cellsPerRow;
        //        int col = cellIndex % cellsPerRow;

        //        int currentPoseX = basePoseX + (col * xOffset);
        //        int currentPoseY = basePoseY + (row * yOffset);

        //        // Get ARM record for this cell
        //        int recordIndex = recordIndicesToUse[cellIndex];
        //        ArmRecord currentRecord = armData.RECORDSET[recordIndex];

        //        // Generate DataMatrix data (with possible duplicate)
        //        bool cellHasDuplicate = false;
        //        string dataMatrixData = GenerateDataMatrixData(currentRecord, cellIndex + 1, isRescanMode, ref cellHasDuplicate);

        //        // Apply or skip errors based on mode
        //        string gtinData = "04603905002474";
        //        string serialNumberData = currentRecord.UN_CODE;
        //        string seriesNameData = "TEST30Х30";
        //        string expireDateData = "06 28";

        //        if (!isRescanMode)
        //        {
        //            // Normal mode: apply random errors with 5% chance
        //            bool cellHasError = false;

        //            dataMatrixData = ApplyRandomError(dataMatrixData, cellIndex + 1, "DataMatrix", ref cellHasError);
        //            gtinData = ApplyRandomError(gtinData, cellIndex + 1, "GTIN", ref cellHasError);
        //            serialNumberData = ApplyRandomError(serialNumberData, cellIndex + 1, "SerialNumber", ref cellHasError);
        //            seriesNameData = ApplyRandomError(seriesNameData, cellIndex + 1, "SeriesName", ref cellHasError);
        //            expireDateData = ApplyRandomError(expireDateData, cellIndex + 1, "ExpireDate", ref cellHasError);

        //            // Дубликат тоже считается ошибкой
        //            if (cellHasError || cellHasDuplicate)
        //            {
        //                currentScanHasErrors = true;
        //            }
        //        }
        //        else
        //        {
        //            // Rescan mode: use clean data (no errors and no duplicates)
        //            Console.WriteLine($"Cell {cellIndex + 1}: Clean rescan of record {recordIndex} (UN_CODE: {currentRecord.UN_CODE})");
        //        }

        //        // Create cell with data
        //        Cell templateCell = CreateCellWithData(cellIndex + 1, currentPoseX, currentPoseY, cellWidth, cellHeight,
        //            dataMatrixData, gtinData, serialNumberData, seriesNameData, expireDateData);

        //        BOX_data dataCell = CreateDataCellFromModel(dataModel, templateCell);
        //        lDMD.Add(dataCell);
        //    }

        //    // Update state for next scan
        //    lock (_lockObject)
        //    {
        //        if (currentScanHasErrors)
        //        {
        //            // Если есть ошибки, следующий скан будет пересканированием
        //            _lastScanHadErrors = true;
        //            Console.WriteLine($"Errors or duplicates detected - next scan will be a RESCAN of the same records (indices {_currentBatchIndices[0]} to {_currentBatchIndices[_currentBatchIndices.Count - 1]})");
        //        }
        //        else
        //        {
        //            // Если ошибок нет, переходим к следующей партии
        //            _lastScanHadErrors = false;

        //            if (!isRescanMode)
        //            {
        //                // Это был обычный скан без ошибок - переходим к следующей партии
        //                _currentBatchStartIndex = (_currentBatchStartIndex + BATCH_SIZE) % armData.RECORDSET.Count;
        //                Console.WriteLine($"Normal scan completed successfully - next scan will process records starting from index {_currentBatchStartIndex}");
        //            }
        //            else
        //            {
        //                // Это был успешный ресcan - переходим к следующей партии
        //                _currentBatchStartIndex = (_currentBatchStartIndex + BATCH_SIZE) % armData.RECORDSET.Count;
        //                Console.WriteLine($"Rescan completed successfully - next scan will process NEW records starting from index {_currentBatchStartIndex}");
        //            }
        //        }
        //    }

        //    result_data dmrd = new result_data();
        //    dmrd.BOXs = lDMD;
        //    string imagePath = Path.Combine(baseDir, "image_raw.jpg");
        //    byte[] imageBytes = File.ReadAllBytes(imagePath);
        //    using (MemoryStream ms = new MemoryStream(imageBytes))
        //    {
        //        dmrd.rawImage = Image.Load<Rgba32>(ms);
        //    }

        //    string modeText = isRescanMode ? "RESCAN" : "NORMAL";
        //    Console.WriteLine($"Add new DM codes - {modeText} mode, processed cells: {lDMD.Count}");
        //    _DMP._dM_recogn_wraper.Update_result_data(dmrd);
        //}
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
            bool shouldStop = false; // Флаг для остановки процесса

            lock (_lockObject)
            {
                if (_lastScanHadErrors)
                {
                    // Режим пересканирования: используем те же записи, что и в предыдущем скане
                    recordIndicesToUse = new List<int>(_currentBatchIndices);
                    isRescanMode = true;
                    _lastScanHadErrors = false; // Сбрасываем флаг ошибок для следующей итерации
                    Console.WriteLine($"RESCAN MODE: Re-processing {recordIndicesToUse.Count} records from indices {recordIndicesToUse[0]} to {recordIndicesToUse[recordIndicesToUse.Count - 1]} WITHOUT errors/duplicates");
                }
                else
                {
                    // Проверяем, не достигли ли мы конца массива
                    if (_currentBatchStartIndex >= armData.RECORDSET.Count)
                    {
                        Console.WriteLine($"Reached end of ARM_JOB_SGTIN records. Total records processed: {armData.RECORDSET.Count}. Stopping...");
                        shouldStop = true;
                    }
                    else
                    {
                        // Обычный режим: берем следующие записи, но не больше чем осталось
                        _currentBatchIndices.Clear();
                        int remainingRecords = armData.RECORDSET.Count - _currentBatchStartIndex;
                        int recordsToProcess = Math.Min(BATCH_SIZE, remainingRecords);

                        for (int i = 0; i < recordsToProcess; i++)
                        {
                            int recordIndex = _currentBatchStartIndex + i;
                            _currentBatchIndices.Add(recordIndex);
                        }
                        recordIndicesToUse = new List<int>(_currentBatchIndices);

                        Console.WriteLine($"NORMAL MODE: Processing records from indices {_currentBatchStartIndex} to {_currentBatchStartIndex + recordsToProcess - 1} (batch start: {_currentBatchStartIndex}, records to process: {recordsToProcess})");

                        // Если это последняя партия записей, помечаем что нужно остановиться после обработки
                        if (_currentBatchStartIndex + recordsToProcess >= armData.RECORDSET.Count)
                        {
                            Console.WriteLine("This is the last batch - process will stop after completion.");
                        }
                    }
                }
            }

            // Если нужно остановиться, выходим из метода
            if (shouldStop)
            {
                return;
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

            // Цикл теперь ограничен количеством доступных записей
            int cellsToProcess = recordIndicesToUse.Count;
            for (int cellIndex = 0; cellIndex < cellsToProcess; cellIndex++)
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
                string seriesNameData = "TEST30X30";
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
                    Console.WriteLine($"Cell {cellIndex + 1}: Clean rescan of record {recordIndex} (UN_CODE: {currentRecord.UN_CODE})");
                }

                // Create cell with data
                Cell templateCell = CreateCellWithData(cellIndex + 1, currentPoseX, currentPoseY, cellWidth, cellHeight,
                    dataMatrixData, gtinData, serialNumberData, seriesNameData, expireDateData);

                BOX_data dataCell = CreateDataCellFromModel(dataModel, templateCell);
                lDMD.Add(dataCell);
            }

            // Update state for next scan
            lock (_lockObject)
            {
                if (currentScanHasErrors)
                {
                    // Если есть ошибки, следующий скан будет пересканированием
                    _lastScanHadErrors = true;
                    Console.WriteLine($"Errors or duplicates detected - next scan will be a RESCAN of the same records (indices {_currentBatchIndices[0]} to {_currentBatchIndices[_currentBatchIndices.Count - 1]})");
                }
                else
                {
                    // Если ошибок нет, переходим к следующей партии (или останавливаемся)
                    _lastScanHadErrors = false;

                    if (!isRescanMode)
                    {
                        // Это был обычный скан без ошибок - переходим к следующей партии
                        _currentBatchStartIndex += cellsToProcess;

                        if (_currentBatchStartIndex >= armData.RECORDSET.Count)
                        {
                            Console.WriteLine($"All records processed successfully. Total records: {armData.RECORDSET.Count}. Process completed.");
                        }
                        else
                        {
                            Console.WriteLine($"Normal scan completed successfully - next scan will process records starting from index {_currentBatchStartIndex}");
                        }
                    }
                    else
                    {
                        // Это был успешный ресcan - переходим к следующей партии
                        _currentBatchStartIndex += cellsToProcess;

                        if (_currentBatchStartIndex >= armData.RECORDSET.Count)
                        {
                            Console.WriteLine($"Rescan completed successfully - all records processed. Total records: {armData.RECORDSET.Count}. Process completed.");
                        }
                        else
                        {
                            Console.WriteLine($"Rescan completed successfully - next scan will process NEW records starting from index {_currentBatchStartIndex}");
                        }
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
                Console.WriteLine($"Cell {cellIndex} - {dataType} error: '{originalData}' -> '{corrupted}'");
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
                if (_currentBatchStartIndex >= armData.RECORDSET.Count)
                {
                    // If we've used all records, start over
                    _currentBatchStartIndex = 0;
                }

                if (armData.RECORDSET.Count == 0)
                {
                    return null;
                }

                ArmRecord record = armData.RECORDSET[_currentBatchStartIndex];
                _currentBatchStartIndex++;

                Console.WriteLine($"Using ARM record {_currentBatchStartIndex}/{armData.RECORDSET.Count}: UN_CODE={record.UN_CODE}");
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