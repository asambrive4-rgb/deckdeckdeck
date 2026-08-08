using System.Windows;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.Views;

namespace DeckDeckDeck.App.ViewModels;

public interface ISlotTimerCoordinator : ITimerActionGateway
{
    void HandleTimerSlotClick(SlotKey slotKey);
}

public sealed class SlotTimerCoordinator : ISlotTimerCoordinator
{
    private readonly ISlotTimerUseCases _timerUseCases;

    public SlotTimerCoordinator(ISlotTimerUseCases timerUseCases)
    {
        _timerUseCases = timerUseCases ?? throw new ArgumentNullException(nameof(timerUseCases));
    }

    public void ExecuteTimerAction(SlotKey slotKey)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => HandleTimerSlotClick(slotKey));
        }
        else
        {
            HandleTimerSlotClick(slotKey);
        }
    }

    public void HandleTimerSlotClick(SlotKey slotKey)
    {
        if (_timerUseCases.IsRunning(slotKey))
        {
            _timerUseCases.StopTimer(slotKey);
            return;
        }

        var vm = new TimerTimePickerViewModel();
        var mainWindow = Application.Current?.MainWindow;
        var window = new TimerTimePickerWindow(vm)
        {
            Owner = mainWindow
        };

        var result = window.ShowDialog();
        if (result == true && vm.IsValidDuration)
        {
            _timerUseCases.StartTimer(slotKey, vm.TotalDuration);
        }
    }
}
