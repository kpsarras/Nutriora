using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Nutriora;
public partial class MainWindow : Window
{
    private readonly NutritionStore _store = new();
    private bool _initializing = true;
    private bool _dark;
    private int _weightRangeDays = 0;
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
        WaterProgress.Value = Math.Clamp(_store.Water(p.Id,Day) / 2000.0 * 100,0,100);
        WeightsList.ItemsSource = _store.Weights(p.Id).Select(w => $"{w.Day}  ·  {w.Kg:0.#} kg").ToList();
        var entries = _store.Entries(p.Id, Day);
        var total = entries.Sum(e => e.Calories);
        DiaryDailyTotal.Text = $"{total:0} kcal";
        DiaryDailyRemaining.Text = $"Υπόλοιπο: {p.Goal-total:0} kcal";
        DiaryBreakfast.Text = $"{entries.Where(e=>e.Meal=="Πρωινό").Sum(e=>e.Calories):0} kcal";
        DiaryLunch.Text = $"{entries.Where(e=>e.Meal=="Μεσημεριανό").Sum(e=>e.Calories):0} kcal";
        DiaryDinner.Text = $"{entries.Where(e=>e.Meal=="Βραδινό").Sum(e=>e.Calories):0} kcal";
        DiarySnacks.Text = $"{entries.Where(e=>e.Meal=="Δεκατιανό" || e.Meal=="Απογευματινό").Sum(e=>e.Calories):0} kcal";
        DiaryWater.Text = $"{_store.Water(p.Id,Day)} ml";
        DiaryWaterProgress.Value = Math.Clamp(_store.Water(p.Id,Day) / 2000.0 * 100,0,100);
        DiaryMacroSummary.Text = $"Πρωτεΐνη: {entries.Sum(e=>e.Protein):0.#} g\nΥδατάνθρακες: {entries.Sum(e=>e.Carbs):0.#} g\nΛίπος: {entries.Sum(e=>e.Fat):0.#} g";
        ActivitySummary.Text = entries.Count == 0 ? "Δεν υπάρχουν γεύματα σήμερα" : $"{entries.Count} καταχωρίσεις  ·  {total:0} kcal";
        DiaryBreakfastFoods.Text = MealDetails(entries.Where(e => e.Meal == "Πρωινό").Select(e=>e.Food));
        DiaryLunchFoods.Text = MealDetails(entries.Where(e => e.Meal == "Μεσημεριανό").Select(e=>e.Food));
        DiaryDinnerFoods.Text = MealDetails(entries.Where(e => e.Meal == "Βραδινό").Select(e=>e.Food));
        DiarySnackFoods.Text = MealDetails(entries.Where(e => e.Meal == "Δεκατιανό" || e.Meal == "Απογευματινό").Select(e=>e.Food));
        RenderActivityChart(entries.Select(e=>e.Calories).ToList());

        BudgetPercentText.Text = $"{(p.Goal > 0 ? total / p.Goal * 100 : 0):0}%";
        BudgetProgress.Value = p.Goal > 0 ? Math.Clamp(total / p.Goal * 100,0,100) : 0;
        DrawBudgetArc(BudgetProgress.Value);
        BudgetRemainingText.Text = $"Απομένουν {p.Goal-total:0} kcal";
        CarbValue.Text = $"{entries.Sum(e=>e.Carbs):0.#} g";
        ProteinValue.Text = $"{entries.Sum(e=>e.Protein):0.#} g";
        FatValue.Text = $"{entries.Sum(e=>e.Fat):0.#} g";
        BreakfastCalories.Text = $"{entries.Where(e=>e.Meal == "Πρωινό").Sum(e=>e.Calories):0} kcal";
        LunchCalories.Text = $"{entries.Where(e=>e.Meal == "Μεσημεριανό").Sum(e=>e.Calories):0} kcal";
        DinnerCalories.Text = $"{entries.Where(e=>e.Meal == "Βραδινό").Sum(e=>e.Calories):0} kcal";
        RenderWeightChart(p.Id);
        CaloriesText.Text = entries.Count == 0 ? $"Δεν υπάρχουν καταχωρίσεις · στόχος {p.Goal:0} kcal" : $"{total:0} / {p.Goal:0} kcal · υπόλοιπο {p.Goal-total:+0;-0;0}";
        CaloriesProgress.Value = p.Goal > 0 ? Math.Clamp(total / p.Goal * 100, 0, 100) : 0;
        MacroText.Text = entries.Count == 0 ? "Μακροθρεπτικά: χωρίς καταγραφή" : $"Πρωτεΐνη {entries.Sum(e=>e.Protein):0.#} g · Υδατάνθρακες {entries.Sum(e=>e.Carbs):0.#} g · Λίπος {entries.Sum(e=>e.Fat):0.#} g";
        SummaryText.Text = entries.Count == 0 ? "Δεν έχουν καταχωριστεί γεύματα." : string.Join(Environment.NewLine, entries.GroupBy(e=>e.Meal).Select(g=>$"{g.Key}: {g.Sum(e=>e.Calories):0} kcal"));
        EntriesList.ItemsSource = entries.Select(e=>new DiaryRow(e.Id,$"{e.Meal} · {e.Food} · {e.Grams:0.#} g · {e.Calories:0.#} kcal")).ToList();
    }
    private sealed record DiaryRow(long Id, string Food);
    private void Show(UIElement panel, string title)
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

    private static string MealDetails(IEnumerable<string> foods)
    {
        var names = foods.Take(3).ToArray();
        return names.Length == 0 ? "Δεν υπάρχουν καταχωρίσεις" : string.Join(" · ", names);
    }
    private void ActivityChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initializing && Current is { } p)
            RenderActivityChart(_store.Entries(p.Id, Day).Select(x=>x.Calories).ToList());
    }
    private void RenderActivityChart(IReadOnlyList<double> calories)
    {
        if (ActivityChart == null) return;
        ActivityChart.Children.Clear();
        double w = Math.Max(140, ActivityChart.ActualWidth), h = Math.Max(45, ActivityChart.ActualHeight);
        var baseline = new Line { X1=0, X2=w, Y1=h-4, Y2=h-4, Stroke=new SolidColorBrush(Color.FromRgb(89,112,109)), StrokeThickness=1 };
        ActivityChart.Children.Add(baseline);
        if (calories.Count == 0) return;
        double max = Math.Max(1, calories.Max());
        double barWidth = Math.Max(4, (w-12)/Math.Max(1,calories.Count));
        for (int i=0;i<calories.Count;i++)
        {
            double height = Math.Max(2,(h-12)*calories[i]/max);
            var bar = new Rectangle { Width=Math.Max(3,barWidth-6), Height=height,
                Fill=new SolidColorBrush(i%2==0 ? Color.FromRgb(202,233,132) : Color.FromRgb(114,205,192)),
                RadiusX=3, RadiusY=3 };
            Canvas.SetLeft(bar,6+i*barWidth);
            Canvas.SetTop(bar,h-4-height);
            ActivityChart.Children.Add(bar);
        }
    }
    private void WeightChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initializing && Current is { } p) RenderWeightChart(p.Id);
    }
    private void RenderWeightChart(long profileId)
    {
        if (WeightChart == null || LatestWeightText == null) return;
        var allMeasurements = _store.Weights(profileId).OrderBy(w => w.Day).ToList();
        var measurements = _weightRangeDays == 0 ? allMeasurements : allMeasurements.Where(w => DateTime.TryParseExact(w.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d) && d >= Day.AddDays(-_weightRangeDays) && d <= Day).ToList();
        foreach (var button in new[] { Range7, Range30, Range90, Range180, Range365, RangeAll })
        {
            bool selected = (button == RangeAll && _weightRangeDays == 0) ||
                (int.TryParse(button.Tag?.ToString(), out var days) && days == _weightRangeDays);
            button.Background = new SolidColorBrush(selected ? Color.FromRgb(44,169,141) : Color.FromRgb(218,235,225));
            button.Foreground = new SolidColorBrush(selected ? Colors.White : Color.FromRgb(34,69,58));
        }
        WeightRangeLabel.Text = _weightRangeDays == 0 ? "Όλες οι διαθέσιμες μετρήσεις" : $"Τελευταίες {_weightRangeDays} ημέρες έως {Day:dd/MM/yyyy}";
        LatestWeightText.Text = measurements.Count == 0 ? "— kg" : $"{measurements[^1].Kg:0.#} kg";
        WeightDeltaText.Text = measurements.Count < 2 ? "Πρόσθεσε μετρήσεις για να δεις την εξέλιξη" :
            $"{measurements[^1].Kg - measurements[0].Kg:+0.#;-0.#;0} kg από την πρώτη μέτρηση";
        WeightChart.Children.Clear();
        if (measurements.Count == 0) return;
        double width = Math.Max(300, WeightChart.ActualWidth);
        double height = Math.Max(120, WeightChart.ActualHeight);
        double min = measurements.Min(w => w.Kg) - 1;
        double max = measurements.Max(w => w.Kg) + 1;
        var points = new PointCollection();
        for (int i = 0; i < measurements.Count; i++)
        {
            double x = 20 + (width-40) * (measurements.Count == 1 ? 0.5 : (double)i/(measurements.Count-1));
            double y = 20 + (height-40) * (1-(measurements[i].Kg-min)/(max-min));
            points.Add(new Point(x,y));
        }
        var guide = new Line { X1=20, X2=width-20, Y1=height-18, Y2=height-18, Stroke=new SolidColorBrush(Color.FromRgb(183,204,194)), StrokeThickness=1, StrokeDashArray=new DoubleCollection { 4,4 } };
        WeightChart.Children.Add(guide);
        var maxLabel = new TextBlock { Text=$"{max-1:0.#} kg", FontSize=11, Foreground=new SolidColorBrush(Color.FromRgb(77,116,100)) };
        Canvas.SetLeft(maxLabel,2); Canvas.SetTop(maxLabel,0); WeightChart.Children.Add(maxLabel);
        var minLabel = new TextBlock { Text=$"{min+1:0.#} kg", FontSize=11, Foreground=new SolidColorBrush(Color.FromRgb(77,116,100)) };
        Canvas.SetLeft(minLabel,2); Canvas.SetTop(minLabel,height-34); WeightChart.Children.Add(minLabel);
        var line = new Polyline { Points=points, Stroke=new SolidColorBrush(Color.FromRgb(37,184,132)), StrokeThickness=3, StrokeLineJoin=PenLineJoin.Round };
        WeightChart.Children.Add(line);
        foreach(var point in points)
        {
            var dot = new Ellipse { Width=10, Height=10, Fill=new SolidColorBrush(Color.FromRgb(235,250,244)), Stroke=new SolidColorBrush(Color.FromRgb(37,184,132)), StrokeThickness=2 };
            Canvas.SetLeft(dot,point.X-5); Canvas.SetTop(dot,point.Y-5); WeightChart.Children.Add(dot);
        }
        if (measurements.Count > 0)
        {
            var first = new TextBlock { Text=measurements[0].Day, FontSize=10, Foreground=new SolidColorBrush(Color.FromRgb(77,116,100)) };
            Canvas.SetLeft(first,20); Canvas.SetTop(first,height-15); WeightChart.Children.Add(first);
            if (measurements.Count > 1)
            {
                var last = new TextBlock { Text=measurements[^1].Day, FontSize=10, Foreground=new SolidColorBrush(Color.FromRgb(77,116,100)) };
                Canvas.SetRight(last,15); Canvas.SetTop(last,height-15); WeightChart.Children.Add(last);
            }
        }
    }

    private void WeightRange_Click(object sender,RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(),out var days))
        {
            _weightRangeDays = days;
            if (Current is { } p) RenderWeightChart(p.Id);
        }
    }
    private void DrawBudgetArc(double percentage)
    {
        if (BudgetArc == null) return;
        var fraction = Math.Clamp(percentage / 100.0,0,0.999999);
        if (fraction <= 0) { BudgetArc.Data = Geometry.Empty; return; }
        const double center = 95, radius = 70;
        double start = -Math.PI/2;
        double end = start + 2*Math.PI*fraction;
        var startPoint = new Point(center + radius*Math.Cos(start),center + radius*Math.Sin(start));
        var endPoint = new Point(center + radius*Math.Cos(end),center + radius*Math.Sin(end));
        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment { Point=endPoint, Size=new Size(radius,radius), SweepDirection=SweepDirection.Clockwise, IsLargeArc=fraction>0.5 });
        BudgetArc.Data = new PathGeometry(new[] { figure });
    }
}
