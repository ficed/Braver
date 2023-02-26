//FF7, Braver, Movies, Save

Console.WriteLine("Enter the FF7 folder (the folder that contains FF7.exe):");
string ff7;
while (true) {
    ff7 = Console.ReadLine().TrimEnd(Path.DirectorySeparatorChar);
    if (File.Exists(Path.Combine(ff7, "ff7.exe"))) break;
    Console.WriteLine("That folder doesn't seem to contain FF7.exe - please enter another folder");
}

string movies;
Console.WriteLine("Enter the FF7 movies folder in MP4 format (contains e.g. opening.mp4)");
while (true) {
    movies = Console.ReadLine().TrimEnd(Path.DirectorySeparatorChar);
    if (File.Exists(Path.Combine(movies, "opening.mp4"))) break;
    Console.WriteLine("That folder doesn't seem to contain FF7 movies in mp4 format - please enter another folder");
}

string save;
Console.WriteLine("Enter the folder to save games in (leave blank to save in the Braver folder)");
while (true) {
    save = Console.ReadLine().TrimEnd(Path.DirectorySeparatorChar);
    if (Directory.Exists(save) || string.IsNullOrEmpty(save)) break;
    Console.WriteLine("That folder doesn't exist - please enter another folder");
}
if (string.IsNullOrEmpty(save)) save = ".";

string config = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "braver.cfg");
File.WriteAllLines(config, new[] {
    $"FF7={ff7}",
    $"Movies={movies}",
    $"Save={save}",
    $"Braver=."
});