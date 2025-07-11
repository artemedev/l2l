using Avalonia.Media.Imaging;
using DM_wraper_NS;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.GS1ParserService;
using l2l_aggregator.ViewModels;
using l2l_aggregator.ViewModels.VisualElements;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace l2l_aggregator.Helpers.AggregationHelpers
{
    public class ImageHelper
    {
        public Image<Rgba32> CropImage(Image<Rgba32> source, double xCell, double yCell, double sizeWidth, double sizeHeight, double scaleXObrat, double scaleYObrat, float angleDegrees)
        {
            float x = (float)(xCell * scaleXObrat);
            float y = (float)(yCell * scaleYObrat);
            float width = (float)(sizeWidth * scaleXObrat);
            float height = (float)(sizeHeight * scaleXObrat);

            // 1. Оптимизация для нулевого угла
            if (angleDegrees % 360 == 0)
            {
                var rect = new Rectangle((int)x, (int)y, (int)width, (int)height);
                return source.Clone(ctx => ctx.Crop(rect));
            }

            float centerX = x + width / 2f;
            float centerY = y + height / 2f;
            float radians = -angleDegrees * MathF.PI / 180f;

            // 2. Вычисляем bounding box для повернутой области
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            // Углы относительно центра
            (float, float)[] corners =
            {
                (-halfWidth, -halfHeight),
                ( halfWidth, -halfHeight),
                ( halfWidth,  halfHeight),
                (-halfWidth,  halfHeight)
            };

            // Поворачиваем углы и находим границы
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var (dx, dy) in corners)
            {
                float xRot = dx * cos - dy * sin;
                float yRot = dx * sin + dy * cos;

                minX = Math.Min(minX, xRot);
                maxX = Math.Max(maxX, xRot);
                minY = Math.Min(minY, yRot);
                maxY = Math.Max(maxY, yRot);
            }

            float bbWidth = maxX - minX;
            float bbHeight = maxY - minY;

            // 3. Вычисляем координаты bounding box в исходном изображении
            float bbX = centerX + minX;
            float bbY = centerY + minY;

            // Корректировка границ
            bbX = Math.Clamp(bbX, 0, source.Width);
            bbY = Math.Clamp(bbY, 0, source.Height);
            bbWidth = Math.Clamp(bbWidth, 1, source.Width - bbX);
            bbHeight = Math.Clamp(bbHeight, 1, source.Height - bbY);

            // 4. Работаем только с нужной областью
            var boundingBox = new Rectangle((int)bbX, (int)bbY, (int)bbWidth, (int)bbHeight);
            using var tempImage = source.Clone(ctx => ctx.Crop(boundingBox));

            // 5. Поворачиваем только вырезанную область
            tempImage.Mutate(ctx => ctx.Rotate(-angleDegrees));

            // 6. Вычисляем центральную область после поворота
            int centerXTemp = tempImage.Width / 2;
            int centerYTemp = tempImage.Height / 2;
            var cropRect = new Rectangle(
                centerXTemp - (int)halfWidth,
                centerYTemp - (int)halfHeight,
                (int)width,
                (int)height
            );

            // 7. Корректируем итоговый прямоугольник
            cropRect.X = Math.Max(0, cropRect.X);
            cropRect.Y = Math.Max(0, cropRect.Y);
            cropRect.Width = Math.Min(cropRect.Width, tempImage.Width - cropRect.X);
            cropRect.Height = Math.Min(cropRect.Height, tempImage.Height - cropRect.Y);

            return tempImage.Clone(ctx => ctx.Crop(cropRect));
        }

        //public ObservableCollection<DmCellViewModel> BuildCellViewModels(in result_data dmrData,
        //                                                                double scaleX, double scaleY,
        //                                                                SessionService sessionService,
        //                                                                ObservableCollection<TemplateField> fields,
        //                                                                ArmJobSgtinResponse response,
        //                                                                AggregationViewModel thisModel, int minX, int minY)
        //{
        //    var cells = new ObservableCollection<DmCellViewModel>();
        //    var gs1Parser = new GS1Parser(); // инициализация парсера
        //    var seenDmValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // отслеживание дублей
        //    var sgtinFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        //    {
        //        { "SerialNumber", "UN_CODE" }
        //    };

        //    // Маппинг OCR.Name к свойствам в ArmJobInfoRecord
        //    var infoFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        //    {
        //        { "SeriesName", "SERIES_NAME" },
        //        { "Gtin", "GTIN"},
        //        { "ExpireDate", "EXPIRE_DATE" }
        //    };
        //    foreach (var dmd in dmrData.BOXs)
        //    {
        //        string? dmValue = dmd.DM.data;

        //        var dm_data = new DmSquareViewModel
        //        {
        //            X = dmd.DM.poseX - (dmd.DM.width / 2),
        //            Y = dmd.DM.poseY - (dmd.DM.height / 2),
        //            SizeWidth = dmd.DM.width,
        //            SizeHeight = dmd.DM.height,
        //            Angle = -dmd.DM.alpha,
        //            IsValid = !dmd.DM.isError,
        //            Data = dmd.DM.data ?? string.Empty
        //        };

        //        // Проверка валидности через GS1Parser
        //        bool isGS1Valid = false;
        //        if (!string.IsNullOrWhiteSpace(dm_data.Data))
        //        {
        //            try
        //            {
        //                var parsed = gs1Parser.ParseGTIN(dm_data.Data);

        //                var expectedGtin = sessionService.SelectedTaskInfo?.GTIN;
        //                var expectedSerials = response.RECORDSET
        //                    .Select(r => r.UN_CODE)
        //                    .Where(code => !string.IsNullOrWhiteSpace(code))
        //                    .ToHashSet();

        //                // Проверка валидности по содержимому
        //                isGS1Valid =
        //                    parsed.GS1isCorrect &&
        //                    !string.IsNullOrWhiteSpace(parsed.GTIN) &&
        //                    !string.IsNullOrWhiteSpace(parsed.SerialNumber) &&
        //                    parsed.GTIN == expectedGtin &&
        //                    expectedSerials.Contains(parsed.SerialNumber);

        //                // Проверка на дубликат
        //                if (isGS1Valid)
        //                {
        //                    if (!seenDmValues.Add(dmValue))
        //                    {
        //                        // дубликат найден
        //                        isGS1Valid = false;
        //                    }
        //                }
        //            }
        //            catch
        //            {
        //                isGS1Valid = false;
        //            }
        //        }

        //        // Учитываем валидность GS1
        //        dm_data.IsValid = dm_data.IsValid && isGS1Valid;

        //        var dmVm = new DmCellViewModel(thisModel)
        //        {
        //            X = (dmd.poseX - (dmd.width / 2) - minX) * scaleX,
        //            Y = (dmd.poseY - (dmd.height / 2) - minY) * scaleY,
        //            SizeWidth = dmd.width * scaleX,
        //            SizeHeight = dmd.height * scaleY,
        //            Angle = -dmd.alpha,
        //            Dm_data = dm_data
        //        };

        //        bool allValid = dm_data.IsValid;

        //        foreach (var ocr in dmd.OCR)
        //        {
        //            var validBarcodes = new HashSet<string>();
        //            //  если имя OCR совпадает с текстом — считаем валидным
        //            bool isValid = false;


        //            if (sgtinFieldMap.TryGetValue(ocr.Name, out var sgtinPropName))
        //            {
        //                var propSgtin = typeof(ArmJobSgtinRecord).GetProperty(sgtinPropName);
        //                if (propSgtin != null)
        //                {
        //                    foreach (var r in response.RECORDSET)
        //                    {
        //                        var value = propSgtin.GetValue(r);
        //                        if (value is string str)
        //                            validBarcodes.Add(str);
        //                    }
        //                }
        //            }
        //            else if (infoFieldMap.TryGetValue(ocr.Name, out var infoPropName))
        //            {
        //                var propInfo = typeof(ArmJobInfoRecord).GetProperty(infoPropName);
        //                if (propInfo != null)
        //                {
        //                    var val = propInfo.GetValue(sessionService.SelectedTaskInfo);
        //                    if (val != null)
        //                        validBarcodes.Add(val.ToString());
        //                }
        //            }

        //            if (ocr.Name == ocr.Text)
        //            {
        //                isValid = true;
        //            }
        //            else if (validBarcodes.Contains(ocr.Text))
        //            {
        //                isValid = true;
        //            }

        //            dmVm.OcrCells.Add(new SquareCellViewModel
        //            {
        //                X = ocr.poseX - (ocr.width / 2),
        //                Y = ocr.poseY - (ocr.height / 2),
        //                SizeWidth = ocr.width,
        //                SizeHeight = ocr.height,
        //                Angle = ocr.alpha,
        //                IsValid = isValid,
        //                OcrName = ocr.Name,
        //                OcrText = ocr.Text
        //            });

        //            if (!isValid)
        //                allValid = false;
        //        }

        //        dmVm.IsValid = allValid && dm_data.IsValid;
        //        cells.Add(dmVm);
        //    }

        //    return cells;
        //}


        public ObservableCollection<DmCellViewModel> BuildCellViewModels(in result_data dmrData,
                                                                double scaleX, double scaleY,
                                                                SessionService sessionService,
                                                                ObservableCollection<TemplateField> fields,
                                                                ArmJobSgtinResponse response,
                                                                AggregationViewModel thisModel, int minX, int minY)
        {
            var cells = new ObservableCollection<DmCellViewModel>();
            var gs1Parser = new GS1Parser(); // инициализация парсера
            var currentScanDmValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // отслеживание дублей в текущем скане
            var sgtinFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "SerialNumber", "UN_CODE" }
    };

            // Маппинг OCR.Name к свойствам в ArmJobInfoRecord
            var infoFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "SeriesName", "SERIES_NAME" },
        { "Gtin", "GTIN"},
        { "ExpireDate", "EXPIRE_DATE" }
    };

            foreach (var dmd in dmrData.BOXs)
            {
                string? dmValue = dmd.DM.data;

                var dm_data = new DmSquareViewModel
                {
                    X = dmd.DM.poseX - (dmd.DM.width / 2),
                    Y = dmd.DM.poseY - (dmd.DM.height / 2),
                    SizeWidth = dmd.DM.width,
                    SizeHeight = dmd.DM.height,
                    Angle = -dmd.DM.alpha,
                    IsValid = !dmd.DM.isError,
                    Data = dmd.DM.data ?? string.Empty
                };

                // Проверка валидности через GS1Parser
                bool isGS1Valid = false;
                bool isDuplicateInCurrentScan = false;
                bool isDuplicateInAllScans = false;

                if (!string.IsNullOrWhiteSpace(dm_data.Data))
                {
                    try
                    {
                        var parsed = gs1Parser.ParseGTIN(dm_data.Data);

                        var expectedGtin = sessionService.SelectedTaskInfo?.GTIN;
                        var expectedSerials = response.RECORDSET
                            .Select(r => r.UN_CODE)
                            .Where(code => !string.IsNullOrWhiteSpace(code))
                            .ToHashSet();

                        // Проверка валидности по содержимому
                        isGS1Valid =
                            parsed.GS1isCorrect &&
                            !string.IsNullOrWhiteSpace(parsed.GTIN) &&
                            !string.IsNullOrWhiteSpace(parsed.SerialNumber) &&
                            parsed.GTIN == expectedGtin &&
                            expectedSerials.Contains(parsed.SerialNumber);

                        // Проверка на дубликат в текущем скане
                        if (isGS1Valid)
                        {
                            isDuplicateInCurrentScan = !currentScanDmValues.Add(dmValue);
                        }

                        // Проверка на дубликат во всех предыдущих сканах
                        if (isGS1Valid && !isDuplicateInCurrentScan)
                        {
                            isDuplicateInAllScans = sessionService.AllScannedDmCodes.Contains(dmValue);
                        }

                        // Код считается невалидным, если это дубликат
                        if (isDuplicateInCurrentScan || isDuplicateInAllScans)
                        {
                            isGS1Valid = false;
                        }
                    }
                    catch
                    {
                        isGS1Valid = false;
                    }
                }

                // Учитываем валидность GS1
                dm_data.IsValid = dm_data.IsValid && isGS1Valid;

                var dmVm = new DmCellViewModel(thisModel)
                {
                    X = (dmd.poseX - (dmd.width / 2) - minX) * scaleX,
                    Y = (dmd.poseY - (dmd.height / 2) - minY) * scaleY,
                    SizeWidth = dmd.width * scaleX,
                    SizeHeight = dmd.height * scaleY,
                    Angle = -dmd.alpha,
                    Dm_data = dm_data
                };

                // Добавляем информацию о типе дубликата для отображения в UI
                dmVm.IsDuplicateInCurrentScan = isDuplicateInCurrentScan;
                dmVm.IsDuplicateInAllScans = isDuplicateInAllScans;

                bool allValid = dm_data.IsValid;

                foreach (var ocr in dmd.OCR)
                {
                    var validBarcodes = new HashSet<string>();
                    //  если имя OCR совпадает с текстом — считаем валидным
                    bool isValid = false;

                    if (sgtinFieldMap.TryGetValue(ocr.Name, out var sgtinPropName))
                    {
                        var propSgtin = typeof(ArmJobSgtinRecord).GetProperty(sgtinPropName);
                        if (propSgtin != null)
                        {
                            foreach (var r in response.RECORDSET)
                            {
                                var value = propSgtin.GetValue(r);
                                if (value is string str)
                                    validBarcodes.Add(str);
                            }
                        }
                    }
                    else if (infoFieldMap.TryGetValue(ocr.Name, out var infoPropName))
                    {
                        var propInfo = typeof(ArmJobInfoRecord).GetProperty(infoPropName);
                        if (propInfo != null)
                        {
                            var val = propInfo.GetValue(sessionService.SelectedTaskInfo);
                            if (val != null)
                                validBarcodes.Add(val.ToString());
                        }
                    }

                    if (ocr.Name == ocr.Text)
                    {
                        isValid = true;
                    }
                    else if (validBarcodes.Contains(ocr.Text))
                    {
                        isValid = true;
                    }

                    dmVm.OcrCells.Add(new SquareCellViewModel
                    {
                        X = ocr.poseX - (ocr.width / 2),
                        Y = ocr.poseY - (ocr.height / 2),
                        SizeWidth = ocr.width,
                        SizeHeight = ocr.height,
                        Angle = ocr.alpha,
                        IsValid = isValid,
                        OcrName = ocr.Name,
                        OcrText = ocr.Text
                    });

                    if (!isValid)
                        allValid = false;
                }

                dmVm.IsValid = allValid && dm_data.IsValid;
                cells.Add(dmVm);
            }
            return cells;
        }


        public Image<Rgba32> GetCroppedImage(result_data dmrData, int minX, int minY, int maxX, int maxY)
        {
            int cropWidth = maxX - minX;
            int cropHeight = maxY - minY;

            if (dmrData.rawImage is Image<Rgba32> img)
            {
                try
                {
                    return img.Clone(ctx =>
                        ctx.Crop(new Rectangle(minX, minY, cropWidth, cropHeight))
                    );
                }
                catch
                {
                    return img.Clone(); // fallback в случае ошибки
                }
            }
            else
            {
                throw new InvalidCastException("rawImage не является Image<Rgba32>. Проверь тип в момент распознавания.");
            }
        }

        public Bitmap ConvertToAvaloniaBitmap(Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }


    }
}
