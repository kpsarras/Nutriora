using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nutriora;
public partial class MainWindow : Window
{
    private readonly NutritionStore _store = new();
    private bool _initializing = true;
    public MainWindow()
    {
        InitializeComponent();
        DayPicker.SelectedDate = DateTime.Today;
        FoodPicker.ItemsSource = _store.Foods();
        RefreshProfiles();
        _initializing = false;
        Refresh();
    }
    private Profile? Current => ProfilePicker.SelectedItem as Profile;
    private DateTime Day => DayPicker.SelectedDate ?? DateTime.Today;
    private void RefreshProfiles(long? selectId = null)
    {
        var oldId = selectId ?? Current?.Id;
        var profiles = _store.Profiles();
        ProfilePicker.ItemsSource = profiles;
        ProfilePicker.SelectedItem = profiles.FirstOrDefault(p => p.Id == oldId) ?? profiles.FirstOrDefault();
    }
    private void Refresh()
    {
        if (_initializing || Current is not { } p) return;
        GoalInput.Text = p.Goal.ToString("0", CultureInfo.CurrentCulture);
        var entries = _store.Entries(p.Id, Day);
        var total = entries.Sum(e => e.Calories);
        CaloriesText.Text = entries.Count == 0 ? $"Δεν υπάρχουν καταχωρίσεις · στόχος {p.Goal:0} kcal" : $"{total:0} / {p.Goal:0} kcal · υπόλοιπο {p.Goal-total:+0;-0;0}";
        CaloriesProgress.Value = p.Goal > 0 ? Math.Clamp(total / p.Goal * 100, 0, 100) : 0;
        MacroText.Text = entries.Count == 0 ? "Μακροθρεπτικά: χωρίς καταγραφή" : $"Πρωτεΐνη {entries.Sum(e=>e.Protein):0.#} g · Υδατάνθρακες {entries.Sum(e=>e.Carbs):0.#} g · Λίπος {entries.Sum(e=>e.Fat):0.#} g";
        SummaryText.Text = entries.Count == 0 ? "Δεν έχουν καταχωριστεί γεύματα." : string.Join(Environment.NewLine, entries.GroupBy(e=>e.Meal).Select(g=>$"{g.Key}: {g.Sum(e=>e.Calories):0} kcal"));
        EntriesList.ItemsSource = entries.Select(e=>new DiaryRow(e.Id,$"{e.Meal} · {e.Food} · {e.Grams:0.#} g · {e.Calories:0.#} kcal")).ToList();
    }
    private sealed record DiaryRow(long Id, string Food);
    private void Show(StackPanel panel, string title)
    {
        DashboardPanel.Visibility = panel == DashboardPanel ? Visibility.Visible : Visibility.Collapsed;
        DiaryPanel.Visibility = panel == DiaryPanel ? Visibility.Visible : Visibility.Collapsed;
        ProfilesPanel.Visibility = panel == ProfilesPanel ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = title;
    }
    private void Dashboard_Click(object sender, RoutedEventArgs e) => Show(DashboardPanel,"Η σημερινή σου εικόνα");
    private void Diary_Click(object sender, RoutedEventArgs e) => Show(DiaryPanel,"Ημερολόγιο διατροφής");
    private void Profiles_Click(object sender, RoutedEventArgs e) => Show(ProfilesPanel,"Προφίλ και στόχοι");
    private void Profile_Changed(object sender, SelectionChangedEventArgs e) => Refresh();
    private void Day_Changed(object sender, SelectionChangedEventArgs e) => Refresh();
    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProfileName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show("Πληκτρολόγησε όνομα προφίλ."); return; }
        var id = _store.AddProfile(name); NewProfileName.Clear(); RefreshProfiles(id); Refresh();
    }
    private static bool PositiveNumber(string text, out double number) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number) ||
         double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        && double.IsFinite(number) && number > 0;
    private void SaveGoal_Click(object sender, RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        if (!PositiveNumber(GoalInput.Text, out var goal) || goal < 500 || goal > 10000) { MessageBox.Show("Επίλεξε στόχο από 500 έως 10000 kcal."); return; }
        _store.SetGoal(p.Id,goal); RefreshProfiles(p.Id); Refresh();
    }
    private void SaveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (Current is not { } p || FoodPicker.SelectedItem is not Food food || MealPicker.SelectedItem is not ComboBoxItem meal) { MessageBox.Show("Επίλεξε τρόφιμο και γεύμα."); return; }
        if (!PositiveNumber(GramsInput.Text,out var grams) || grams > 10000) { MessageBox.Show("Η ποσότητα πρέπει να είναι μεταξύ 0 και 10000 γραμμαρίων."); return; }
        _store.AddEntry(p.Id,Day,meal.Content.ToString()!,food.Id,grams); Refresh();
    }
    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (Current is not { } p || EntriesList.SelectedItem is not DiaryRow row) return;
        if (MessageBox.Show("Να διαγραφεί η επιλεγμένη καταχώριση;","Nutriora",MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _store.DeleteEntry(row.Id,p.Id); Refresh();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}