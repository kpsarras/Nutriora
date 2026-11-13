using System.Windows;
namespace Nutriora;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); }
    private void Dashboard_Click(object sender, RoutedEventArgs e) => PageTitle.Text = "Η σημερινή σου εικόνα";
    private void Diary_Click(object sender, RoutedEventArgs e) => PageTitle.Text = "Ημερολόγιο διατροφής";
    private void Settings_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Οι ρυθμίσεις θα ενεργοποιηθούν σε επόμενο build.", "Nutriora");
    private void AddMeal_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Η καταχώριση γευμάτων βρίσκεται υπό ανάπτυξη. Δεν αποθηκεύτηκαν δεδομένα.", "Nutriora");
}