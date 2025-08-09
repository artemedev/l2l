using Avalonia.Media;
using Avalonia.Threading;
using l2l_aggregator.Views.Popup;
using Material.Icons;
using System;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Да", string cancelText = "Отмена");
        Task<bool> ShowExitConfirmationAsync();
        Task<bool> ShowDeleteConfirmationAsync(string itemName = "элемент");
        Task<bool> ShowSaveConfirmationAsync();
        Task<bool> ShowCustomConfirmationAsync(string title, string message, MaterialIconKind icon, IBrush iconColor, IBrush confirmButtonColor, string confirmText = "Да", string cancelText = "Отмена");
        void SetDialogContainer(ConfirmationDialog dialog);
    }

    public class DialogService : IDialogService
    {
        private ConfirmationDialog? _dialogContainer;

        public void SetDialogContainer(ConfirmationDialog dialog)
        {
            _dialogContainer = dialog;
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Да", string cancelText = "Отмена")
        {
            return await ShowDialogSafely(async () =>
            {
                if (_dialogContainer == null)
                    throw new InvalidOperationException("Dialog container не установлен. Вызовите SetDialogContainer сначала.");

                _dialogContainer.Title = title;
                _dialogContainer.Message = message;
                _dialogContainer.ConfirmText = confirmText;
                _dialogContainer.CancelText = cancelText;
                _dialogContainer.IconKind = MaterialIconKind.HelpCircle;
                _dialogContainer.IconColor = Brushes.Orange;
                _dialogContainer.ConfirmButtonColor = Brushes.Blue;

                return await _dialogContainer.ShowAndWaitAsync();
            });
        }

        public async Task<bool> ShowExitConfirmationAsync()
        {
            return await ShowDialogSafely(async () =>
            {
                if (_dialogContainer == null)
                    throw new InvalidOperationException("Dialog container не установлен. Вызовите SetDialogContainer сначала.");

                _dialogContainer.Title = "Подтверждение выхода";
                _dialogContainer.Message = "Вы действительно хотите выйти из системы?";
                _dialogContainer.ConfirmText = "Выйти";
                _dialogContainer.CancelText = "Отмена";
                _dialogContainer.IconKind = MaterialIconKind.ExitToApp;
                _dialogContainer.IconColor = Brushes.Orange;
                _dialogContainer.ConfirmButtonColor = Brushes.IndianRed;

                return await _dialogContainer.ShowAndWaitAsync();
            });
        }

        public async Task<bool> ShowDeleteConfirmationAsync(string itemName = "элемент")
        {
            return await ShowDialogSafely(async () =>
            {
                if (_dialogContainer == null)
                    throw new InvalidOperationException("Dialog container не установлен. Вызовите SetDialogContainer сначала.");

                _dialogContainer.Title = "Подтверждение удаления";
                _dialogContainer.Message = $"Вы действительно хотите удалить {itemName}?";
                _dialogContainer.ConfirmText = "Удалить";
                _dialogContainer.CancelText = "Отмена";
                _dialogContainer.IconKind = MaterialIconKind.Delete;
                _dialogContainer.IconColor = Brushes.Red;
                _dialogContainer.ConfirmButtonColor = Brushes.Red;

                return await _dialogContainer.ShowAndWaitAsync();
            });
        }

        public async Task<bool> ShowSaveConfirmationAsync()
        {
            return await ShowDialogSafely(async () =>
            {
                if (_dialogContainer == null)
                    throw new InvalidOperationException("Dialog container не установлен. Вызовите SetDialogContainer сначала.");

                _dialogContainer.Title = "Сохранение изменений";
                _dialogContainer.Message = "У вас есть несохраненные изменения. Сохранить их?";
                _dialogContainer.ConfirmText = "Сохранить";
                _dialogContainer.CancelText = "Не сохранять";
                _dialogContainer.IconKind = MaterialIconKind.ContentSave;
                _dialogContainer.IconColor = Brushes.Blue;
                _dialogContainer.ConfirmButtonColor = Brushes.Green;

                return await _dialogContainer.ShowAndWaitAsync();
            });
        }

        public async Task<bool> ShowCustomConfirmationAsync(
            string title,
            string message,
            MaterialIconKind icon,
            IBrush iconColor,
            IBrush confirmButtonColor,
            string confirmText = "Да",
            string cancelText = "Отмена")
        {
            return await ShowDialogSafely(async () =>
            {
                if (_dialogContainer == null)
                    throw new InvalidOperationException("Dialog container не установлен. Вызовите SetDialogContainer сначала.");

                _dialogContainer.Title = title;
                _dialogContainer.Message = message;
                _dialogContainer.ConfirmText = confirmText;
                _dialogContainer.CancelText = cancelText;
                _dialogContainer.IconKind = icon;
                _dialogContainer.IconColor = iconColor;
                _dialogContainer.ConfirmButtonColor = confirmButtonColor;

                return await _dialogContainer.ShowAndWaitAsync();
            });
        }

        // Вспомогательный метод для безопасного показа диалогов в UI потоке
        private async Task<bool> ShowDialogSafely(Func<Task<bool>> dialogAction)
        {
            try
            {
                // Проверяем, находимся ли мы в UI потоке
                if (Dispatcher.UIThread.CheckAccess())
                {
                    // Мы уже в UI потоке, выполняем действие напрямую
                    return await dialogAction();
                }
                else
                {
                    // Мы не в UI потоке, диспетчеризуем в UI поток
                    return await Dispatcher.UIThread.InvokeAsync(dialogAction);
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки возвращаем false (отмена)
                System.Diagnostics.Debug.WriteLine($"Ошибка показа диалога: {ex.Message}");
                return false;
            }
        }
    }
}