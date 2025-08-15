using Avalonia.Media.Imaging;
using Avalonia.Threading;
using l2l_aggregator.ViewModels.VisualElements;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.AggregationService
{
    public class CellProcessingService
    {
        private const int UI_DELAY = 100;

        private readonly ImageProcessorService _imageProcessingService;
        private readonly TextGenerationService _textGenerationService;

        public CellProcessingService(
            ImageProcessorService imageProcessingService,
            TextGenerationService textGenerationService)
        {
            _imageProcessingService = imageProcessingService;
            _textGenerationService = textGenerationService;
        }

        public async Task<Bitmap> ProcessCellImageAsync(
            DmCellViewModel cell,
            Image<Rgba32> croppedImageRaw,
            double scaleXObrat,
            double scaleYObrat)
        {
            var cropped = _imageProcessingService.CropImage(
                croppedImageRaw, cell.X, cell.Y, cell.SizeWidth, cell.SizeHeight,
                scaleXObrat, scaleYObrat, (float)cell.Angle);

            Bitmap selectedSquareImage = null;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                selectedSquareImage = _imageProcessingService.ConvertToAvaloniaBitmap(cropped);
                await Task.Delay(UI_DELAY);
            });

            return selectedSquareImage;
        }

        public void UpdateCellPopupData(DmCellViewModel cell, Bitmap selectedSquareImage, Avalonia.Size imageSizeCell)
        {
            var scaleXCell = imageSizeCell.Width / selectedSquareImage.PixelSize.Width;
            var scaleYCell = imageSizeCell.Height / selectedSquareImage.PixelSize.Height;

            var newOcrList = CreateScaledOcrList(cell, scaleXCell, scaleYCell);

            cell.OcrCellsInPopUp.Clear();
            foreach (var newOcr in newOcrList)
                cell.OcrCellsInPopUp.Add(newOcr);
        }

        public string DisplayCellInformation(DmCellViewModel cell)
        {
            return _textGenerationService.BuildCellInfoSummary(cell);
        }

        private ObservableCollection<SquareCellViewModel> CreateScaledOcrList(DmCellViewModel cell, double scaleXCell, double scaleYCell)
        {
            var newOcrList = new ObservableCollection<SquareCellViewModel>();

            // Добавляем OCR элементы
            foreach (var ocr in cell.OcrCells)
            {
                newOcrList.Add(new SquareCellViewModel
                {
                    X = ocr.X * scaleXCell,
                    Y = ocr.Y * scaleYCell,
                    SizeWidth = ocr.SizeWidth * scaleXCell,
                    SizeHeight = ocr.SizeHeight * scaleYCell,
                    IsValid = ocr.IsValid,
                    Angle = ocr.Angle,
                    OcrName = ocr.OcrName,
                    OcrText = ocr.OcrText
                });
            }

            // Добавляем DM элемент (если есть)
            if (cell.Dm_data.Data != null)
            {
                newOcrList.Add(new SquareCellViewModel
                {
                    X = cell.Dm_data.X * scaleXCell,
                    Y = cell.Dm_data.Y * scaleYCell,
                    SizeWidth = cell.Dm_data.SizeWidth * scaleYCell,
                    SizeHeight = cell.Dm_data.SizeHeight * scaleYCell,
                    IsValid = cell.Dm_data.IsValid,
                    Angle = cell.Dm_data.Angle,
                    OcrName = "DM",
                    OcrText = cell.Dm_data.Data ?? "пусто"
                });
            }

            return newOcrList;
        }
    }
}