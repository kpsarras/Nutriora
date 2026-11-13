using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nutriora;
public partial class MainWindow : Window
{
    private readonly NutritionStore _store = new();
    private bool _initializing = true;
    private bool _dark;
    public MainWindow()
    {
        InitializeComponent();
        DayPicker.SelectedDate = DateTime.Today;
        RefreshFoods();
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
        WaterText.Text = $"{_store.Water(p.Id,Day)} ml";
        WeightsList.ItemsSource = _store.Weights(p.Id).Select(w => $"{w.Day}  ·  {w.Kg:0.#} kg").ToList();
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
        WeightPanel.Visibility = panel == WeightPanel ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = title;
    }
    private void Dashboard_Click(object sender, RoutedEventArgs e) => Show(DashboardPanel,"Η σημερινή σου εικόνα");
    private void Diary_Click(object sender, RoutedEventArgs e) => Show(DiaryPanel,"Ημερολόγιο διατροφής");
    private void Profiles_Click(object sender, RoutedEventArgs e) => Show(ProfilesPanel,"Προφίλ και στόχοι");
    private void Weight_Click(object sender, RoutedEventArgs e) => Show(WeightPanel,"Ιστορικό βάρους");
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

    private void RefreshFoods()
    {
        var previous = (FoodPicker.SelectedItem as Food)?.Id;
        var search = FoodSearch?.Text?.Trim() ?? "";
        var foods = _store.Foods().Where(f => f.Name.Contains(search,StringComparison.CurrentCultureIgnoreCase)).ToList();
        FoodPicker.ItemsSource = foods;
        FoodPicker.SelectedItem = foods.FirstOrDefault(f => f.Id == previous) ?? foods.FirstOrDefault();
    }
    private void FoodSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        RefreshFoods();
    }
    private void AddFood_Click(object sender, RoutedEventArgs e)
    {
        var name = CustomFoodName.Text.Trim();
        if (name.Length < 2 || name.Length > 100 ||
            !Nonnegative(CustomKcal.Text,out var kcal) || !Nonnegative(CustomProtein.Text,out var protein) ||
            !Nonnegative(CustomCarbs.Text,out var carbs) || !Nonnegative(CustomFat.Text,out var fat) ||
            kcal > 900 || protein > 100 || carbs > 100 || fat > 100)
        {
            MessageBox.Show("Συμπλήρωσε όνομα και έγκυρες τιμές ανά 100 g."); return;
        }
        try { _store.AddFood(name,kcal,protein,carbs,fat); }
        catch (SqliteException) { MessageBox.Show("Υπάρχει ήδη τρόφιμο με αυτό το όνομα."); return; }
        CustomFoodName.Clear(); FoodSearch.Clear(); RefreshFoods();
    }
    private static bool Nonnegative(string text,out double number) =>
        (double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out number) ||
        double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number))
        && double.IsFinite(number) && number >= 0;
    private void UpdateEntry_Click(object sender,RoutedEventArgs e)
    {
        if (Current is not { } p || EntriesList.SelectedItem is not DiaryRow row ||
            FoodPicker.SelectedItem is not Food food || MealPicker.SelectedItem is not ComboBoxItem meal) {
            MessageBox.Show("Επίλεξε καταχώριση, τρόφιμο και γεύμα."); return;
        }
        if (!PositiveNumber(GramsInput.Text,out var grams) || grams > 10000) {
            MessageBox.Show("Μη έγκυρη ποσότητα."); return;
        }
        _store.UpdateEntry(row.Id,p.Id,food.Id,meal.Content.ToString()!,grams); Refresh();
    }
    private void CopyYesterday_Click(object sender,RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        if (MessageBox.Show("Αντιγραφή όλων των χθεσινών γευμάτων στην επιλεγμένη ημέρα;","Nutriora",MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _store.CopyEntries(p.Id,Day.AddDays(-1),Day); Refresh();
    }
    private void Rename_Click(object sender,RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        var name = RenameInput.Text.Trim();
        if (name.Length == 0 || name.Length > 60) { MessageBox.Show("Συμπλήρωσε έγκυρο όνομα."); return; }
        _store.RenameProfile(p.Id,name); RefreshProfiles(p.Id); Refresh(); RenameInput.Clear();
    }
    private void Water250_Click(object sender,RoutedEventArgs e) => ChangeWater(250);
    private void Water500_Click(object sender,RoutedEventArgs e) => ChangeWater(500);
    private void WaterMinus_Click(object sender,RoutedEventArgs e) => ChangeWater(-250);
    private void ChangeWater(int ml)
    {
        if (Current is not { } p) return;
        _store.AddWater(p.Id,Day,ml); Refresh();
    }
    private void SaveWeight_Click(object sender,RoutedEventArgs e)
    {
        if (Current is not { } p) return;
        if (!PositiveNumber(WeightInput.Text,out var kg) || kg < 10 || kg > 500) { MessageBox.Show("Συμπλήρωσε έγκυρο βάρος σε kg."); return; }
        _store.SetWeight(p.Id,Day,kg); Refresh(); WeightInput.Clear();
    }
    private void Theme_Click(object sender,RoutedEventArgs e)
    {
        _dark = !_dark;
        var resources = Application.Current.Resources;
        resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_dark ? "#15231F" : "#F8F5EE"));
        resources["PanelBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_dark ? "#23382F" : "#FFFFFF"));
        resources["InkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_dark ? "#E5F2E8" : "#173D35"));
        resources["MutedBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_dark ? "#B6CFC0" : "#60786F"));
    }
}
