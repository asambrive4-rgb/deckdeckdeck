using System.Diagnostics;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;
using Xunit.Abstractions;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

/// <summary>
/// Real wall-clock timings for navigation cache (plan B).
/// Not a hard performance gate — logs numbers and checks reuse + relative warm/cold trend.
/// </summary>
public sealed class NavigationCacheTimingTests
{
    private readonly ITestOutputHelper _output;

    public NavigationCacheTimingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WarmHomeAndCategoryReuseIsFasterThanColdCreate()
    {
        var services = CreateServices();
        SeedRealisticGrid(services);

        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(9, home.NumpadGrid.Slots.Count(s => !s.IsEmpty));

        // Cold: fresh MainViewModel each time → always CreateCategory + DB.
        var coldSamples = new List<double>(7);
        for (var i = 0; i < 7; i++)
        {
            var coldVm = CreateMainViewModel(services);
            var sw = Stopwatch.StartNew();
            coldVm.OpenCategoryFromHotkey(SlotKey.Numpad1);
            sw.Stop();
            Assert.IsType<CategoryViewModel>(coldVm.CurrentViewModel);
            coldSamples.Add(sw.Elapsed.TotalMilliseconds);
            coldVm.Dispose();
        }

        // Warm: long-lived VM → cached Home/Category reuse.
        var warmVm = CreateMainViewModel(services);
        var warmHome = Assert.IsType<HomeViewModel>(warmVm.CurrentViewModel);
        warmVm.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(warmVm.CurrentViewModel);

        var warmBackHomeSamples = new List<double>(7);
        var warmReopenSamples = new List<double>(7);
        for (var i = 0; i < 7; i++)
        {
            var swHome = Stopwatch.StartNew();
            category.BackCommand.Execute(null);
            swHome.Stop();
            Assert.Same(warmHome, warmVm.CurrentViewModel);
            warmBackHomeSamples.Add(swHome.Elapsed.TotalMilliseconds);

            var swOpen = Stopwatch.StartNew();
            warmVm.OpenCategoryFromHotkey(SlotKey.Numpad1);
            swOpen.Stop();
            var reopened = Assert.IsType<CategoryViewModel>(warmVm.CurrentViewModel);
            Assert.Same(category, reopened);
            warmReopenSamples.Add(swOpen.Elapsed.TotalMilliseconds);
        }

        warmVm.Dispose();

        var coldMedian = Median(coldSamples);
        var warmHomeMedian = Median(warmBackHomeSamples);
        var warmOpenMedian = Median(warmReopenSamples);

        _output.WriteLine($"cold OpenCategory median={coldMedian:F2}ms samples=[{string.Join(", ", coldSamples.Select(v => v.ToString("F2")))}]");
        _output.WriteLine($"warm Back→Home median={warmHomeMedian:F2}ms samples=[{string.Join(", ", warmBackHomeSamples.Select(v => v.ToString("F2")))}]");
        _output.WriteLine($"warm ReopenCategory median={warmOpenMedian:F2}ms samples=[{string.Join(", ", warmReopenSamples.Select(v => v.ToString("F2")))}]");

        // Warm path must not be slower than cold create (equal ok when both near 0).
        Assert.True(
            warmOpenMedian <= coldMedian + 0.5,
            $"Expected warm reopen ({warmOpenMedian:F2}ms) <= cold open ({coldMedian:F2}ms).");
        Assert.True(
            warmHomeMedian <= coldMedian + 0.5,
            $"Expected warm home ({warmHomeMedian:F2}ms) <= cold open ({coldMedian:F2}ms).");
    }

    [Fact]
    public void SaveInvalidationRebuildsAndStillCompletesQuickly()
    {
        var services = CreateServices();
        SeedRealisticGrid(services);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        category.NumpadGrid.Numpad3.EditCommand.Execute(null);
        var editor = Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        editor.SnippetTitle = "Renamed Timing Snippet";

        var sw = Stopwatch.StartNew();
        editor.SaveCommand.Execute(null);
        sw.Stop();

        var refreshed = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.NotSame(category, refreshed);
        Assert.Equal("Renamed Timing Snippet", refreshed.NumpadGrid.Numpad3.Title);

        _output.WriteLine($"snippet save + category rebuild = {sw.Elapsed.TotalMilliseconds:F2}ms");
        // Soft ceiling: rebuild should stay interactive on CI/dev machines.
        Assert.True(
            sw.Elapsed.TotalMilliseconds < 2000,
            $"Rebuild took too long: {sw.Elapsed.TotalMilliseconds:F2}ms");
    }

    private static void SeedRealisticGrid(TestServices services)
    {
        var slotKeys = new[]
        {
            SlotKey.Numpad1, SlotKey.Numpad2, SlotKey.Numpad3,
            SlotKey.Numpad4, SlotKey.Numpad5, SlotKey.Numpad6,
            SlotKey.Numpad7, SlotKey.Numpad8, SlotKey.Numpad9
        };

        for (var i = 0; i < slotKeys.Length; i++)
        {
            var category = services.CategoryRepository.Create(
                slotKeys[i],
                $"Category {i + 1}",
                $"Description {i + 1}");

            var snippetSlots = new[]
            {
                SlotKey.Numpad1, SlotKey.Numpad2, SlotKey.Numpad3,
                SlotKey.Numpad4, SlotKey.Numpad5, SlotKey.Numpad6
            };
            foreach (var snippetSlot in snippetSlots)
            {
                services.SnippetRepository.Create(
                    category.Id,
                    snippetSlot,
                    $"Snippet {i + 1}-{snippetSlot.GetDisplayText()}",
                    $"Body for {i + 1}-{snippetSlot.GetDisplayText()}",
                    description: null);
            }
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        return ordered[ordered.Length / 2];
    }
}
