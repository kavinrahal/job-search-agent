namespace JobSearch.Data;

public static class SkillLoader
{
    public static string Load(string filename)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            string path = Path.Combine(dir.FullName, "skills", filename);
            if (File.Exists(path)) return File.ReadAllText(path);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"skills/{filename} not found in any ancestor directory.");
    }
}
