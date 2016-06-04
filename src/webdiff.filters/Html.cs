using System.IO;
using System.Text.RegularExpressions;

public static class Html
{
    public static void RemoveComments(string file)
    {
        string fileText = File.ReadAllText(file);
        Regex rx = new Regex("<!--(.*?)-->");
        fileText = rx.Replace(fileText, "");
        File.WriteAllText(file, fileText);
    }
}