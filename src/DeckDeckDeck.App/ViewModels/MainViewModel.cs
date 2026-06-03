using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private object _currentViewModel = null!;
    private string _statusMessage = "Ready.";

    public MainViewModel()
    {
        ShowHome();
    }

    public string WindowTitle => "DeckDeckDeck";

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void ShowHome()
    {
        CurrentViewModel = new HomeViewModel(OpenCategory, ShowStatus);
        StatusMessage = "Home";
    }

    public void SelectSlot(SlotKey slotKey)
    {
        switch (CurrentViewModel)
        {
            case HomeViewModel homeViewModel:
                homeViewModel.SelectSlot(slotKey);
                break;
            case CategoryViewModel categoryViewModel:
                categoryViewModel.SelectSlot(slotKey);
                break;
        }
    }

    private void OpenCategory(SlotKey slotKey, string categoryName)
    {
        CurrentViewModel = new CategoryViewModel(slotKey, categoryName, ShowHome, ShowStatus);
        StatusMessage = $"{categoryName} category";
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
    }
}
