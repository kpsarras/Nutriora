using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;

namespace Nutriora;

public sealed record Profile(long Id, string Name, double Goal);
public sealed record Food(long Id, string Name, double Calories, double Protein, double Carbs, double Fat);
public sealed record DiaryEntry(long Id, string Meal, string Food, double Grams, double Calories, double Protein, double Carbs, double Fat);

public sealed record WeightEntry(string Day, double Kg);

public sealed class NutritionStore
{
    private readonly string _connectionString;
    public NutritionStore()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nutriora");
        Directory.CreateDirectory(folder);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "nutriora.db") }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS profiles (id INTEGER PRIMARY KEY, name TEXT NOT NULL, goal REAL NOT NULL DEFAULT 2000);
            CREATE TABLE IF NOT EXISTS foods (id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE, kcal REAL NOT NULL, protein REAL NOT NULL, carbs REAL NOT NULL, fat REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS entries (id INTEGER PRIMARY KEY, profile_id INTEGER NOT NULL REFERENCES profiles(id), day TEXT NOT NULL, meal TEXT NOT NULL, food_id INTEGER NOT NULL REFERENCES foods(id), grams REAL NOT NULL CHECK(grams > 0));
            CREATE TABLE IF NOT EXISTS water (profile_id INTEGER NOT NULL, day TEXT NOT NULL, ml INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(profile_id,day));
            CREATE TABLE IF NOT EXISTS weights (profile_id INTEGER NOT NULL, day TEXT NOT NULL, kg REAL NOT NULL, PRIMARY KEY(profile_id,day));
            """;
        cmd.ExecuteNonQuery();
        if (Count("profiles") == 0) Execute("INSERT INTO profiles(name,goal) VALUES ('Προφίλ 1',2000)");
        if (Count("foods") == 0)
        {
            foreach (var food in new [] {
                ("Μήλο",52d,0.3d,13.8d,0.2d),("Μπανάνα",89d,1.1d,22.8d,0.3d),
                ("Γιαούρτι στραγγιστό 2%",73d,10d,3.5d,2d),("Αυγό βραστό",155d,12.6d,1.1d,10.6d),
                ("Ρύζι βρασμένο",130d,2.7d,28d,0.3d),("Στήθος κοτόπουλου ψημένο",165d,31d,0d,3.6d),
                ("Ντομάτα",18d,0.9d,3.9d,0.2d),("Ψωμί ολικής",247d,13d,41d,4.2d)
            })
            {
                using var insert = db.CreateCommand();
                insert.CommandText = "INSERT INTO foods(name,kcal,protein,carbs,fat) VALUES ($n,$k,$p,$c,$f)";
                insert.Parameters.AddWithValue("$n", food.Item1);
                insert.Parameters.AddWithValue("$k", food.Item2);
                insert.Parameters.AddWithValue("$p", food.Item3);
                insert.Parameters.AddWithValue("$c", food.Item4);
                insert.Parameters.AddWithValue("$f", food.Item5);
                insert.ExecuteNonQuery();
            }
        }
    }
    private SqliteConnection Open() { var c = new SqliteConnection(_connectionString); c.Open(); using var fk = c.CreateCommand(); fk.CommandText = "PRAGMA foreign_keys=ON"; fk.ExecuteNonQuery(); return c; }
    private int Count(string table) { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = table == "foods" ? "SELECT COUNT(*) FROM foods" : "SELECT COUNT(*) FROM profiles"; return Convert.ToInt32(c.ExecuteScalar()); }
    private void Execute(string sql) { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
    public List<Profile> Profiles()
    {
        using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "SELECT id,name,goal FROM profiles ORDER BY id";
        using var r = c.ExecuteReader(); var list = new List<Profile>();
        while (r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetDouble(2)));
        return list;
    }
    public long AddProfile(string name)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO profiles(name,goal) VALUES ($n,2000); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$n", name); return (long)(c.ExecuteScalar() ?? 0L);
    }
    public void SetGoal(long profileId, double goal)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "UPDATE profiles SET goal=$g WHERE id=$id";
        c.Parameters.AddWithValue("$g",goal); c.Parameters.AddWithValue("$id",profileId); c.ExecuteNonQuery();
    }
    public List<Food> Foods()
    {
        using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "SELECT id,name,kcal,protein,carbs,fat FROM foods ORDER BY name";
        using var r = c.ExecuteReader(); var list = new List<Food>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetDouble(2),r.GetDouble(3),r.GetDouble(4),r.GetDouble(5)));
        return list;
    }
    public void AddEntry(long profileId, DateTime day, string meal, long foodId, double grams)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO entries(profile_id,day,meal,food_id,grams) VALUES ($p,$d,$m,$f,$g)";
        c.Parameters.AddWithValue("$p",profileId); c.Parameters.AddWithValue("$d",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        c.Parameters.AddWithValue("$m",meal); c.Parameters.AddWithValue("$f",foodId); c.Parameters.AddWithValue("$g",grams); c.ExecuteNonQuery();
    }
    public void DeleteEntry(long entryId, long profileId)
    {
        using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "DELETE FROM entries WHERE id=$id AND profile_id=$p";
        c.Parameters.AddWithValue("$id",entryId); c.Parameters.AddWithValue("$p",profileId); c.ExecuteNonQuery();
    }
    public List<DiaryEntry> Entries(long profileId, DateTime day)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = """
            SELECT e.id,e.meal,f.name,e.grams,ROUND(f.kcal*e.grams/100.0,1),
            ROUND(f.protein*e.grams/100.0,1),ROUND(f.carbs*e.grams/100.0,1),ROUND(f.fat*e.grams/100.0,1)
            FROM entries e JOIN foods f ON f.id=e.food_id WHERE e.profile_id=$p AND e.day=$d ORDER BY e.id DESC
            """;
        c.Parameters.AddWithValue("$p",profileId); c.Parameters.AddWithValue("$d",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        using var r = c.ExecuteReader(); var list = new List<DiaryEntry>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetDouble(3),r.GetDouble(4),r.GetDouble(5),r.GetDouble(6),r.GetDouble(7)));
        return list;
    }

    public void RenameProfile(long id, string name)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "UPDATE profiles SET name=$n WHERE id=$id";
        c.Parameters.AddWithValue("$n",name); c.Parameters.AddWithValue("$id",id); c.ExecuteNonQuery();
    }
    public void AddFood(string name,double kcal,double protein,double carbs,double fat)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO foods(name,kcal,protein,carbs,fat) VALUES ($n,$k,$p,$c,$f)";
        c.Parameters.AddWithValue("$n",name); c.Parameters.AddWithValue("$k",kcal);
        c.Parameters.AddWithValue("$p",protein); c.Parameters.AddWithValue("$c",carbs); c.Parameters.AddWithValue("$f",fat);
        c.ExecuteNonQuery();
    }
    public void UpdateEntry(long id,long profileId,long foodId,string meal,double grams)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "UPDATE entries SET food_id=$f,meal=$m,grams=$g WHERE id=$id AND profile_id=$p";
        c.Parameters.AddWithValue("$f",foodId); c.Parameters.AddWithValue("$m",meal); c.Parameters.AddWithValue("$g",grams);
        c.Parameters.AddWithValue("$id",id); c.Parameters.AddWithValue("$p",profileId); c.ExecuteNonQuery();
    }
    public void CopyEntries(long profileId,DateTime source,DateTime target)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO entries(profile_id,day,meal,food_id,grams) SELECT profile_id,$to,meal,food_id,grams FROM entries WHERE profile_id=$p AND day=$from";
        c.Parameters.AddWithValue("$p",profileId);
        c.Parameters.AddWithValue("$from",source.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        c.Parameters.AddWithValue("$to",target.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        c.ExecuteNonQuery();
    }
    public int Water(long profileId,DateTime day)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "SELECT ml FROM water WHERE profile_id=$p AND day=$d";
        c.Parameters.AddWithValue("$p",profileId); c.Parameters.AddWithValue("$d",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        return Convert.ToInt32(c.ExecuteScalar() ?? 0);
    }
    public void AddWater(long profileId,DateTime day,int ml)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO water(profile_id,day,ml) VALUES($p,$d,$ml) ON CONFLICT(profile_id,day) DO UPDATE SET ml=MAX(0,ml+excluded.ml)";
        c.Parameters.AddWithValue("$p",profileId); c.Parameters.AddWithValue("$d",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        c.Parameters.AddWithValue("$ml",ml); c.ExecuteNonQuery();
    }
    public void SetWeight(long profileId,DateTime day,double kg)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "INSERT INTO weights(profile_id,day,kg) VALUES($p,$d,$kg) ON CONFLICT(profile_id,day) DO UPDATE SET kg=excluded.kg";
        c.Parameters.AddWithValue("$p",profileId); c.Parameters.AddWithValue("$d",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        c.Parameters.AddWithValue("$kg",kg); c.ExecuteNonQuery();
    }
    public List<WeightEntry> Weights(long profileId)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "SELECT day,kg FROM weights WHERE profile_id=$p ORDER BY day DESC LIMIT 30";
        c.Parameters.AddWithValue("$p",profileId);
        using var r = c.ExecuteReader(); var list = new List<WeightEntry>();
        while(r.Read()) list.Add(new(r.GetString(0),r.GetDouble(1)));
        return list;
    }
}
