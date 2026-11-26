using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using SNSRModLoader.Updater;

internal static class Program
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
        string dll = Path.Combine(root, "dist", "SubnauticaSpeedrunningMod.dll");
        string loader = Path.GetFullPath(Path.Combine(root, "..", "SNSRModLoader", "artifacts", "package", "Mods", "Loader"));
        using (var assembly = AssemblyDefinition.ReadAssembly(dll))
        {
            var registration = assembly.MainModule.Types.SelectMany(t => t.CustomAttributes)
                .Single(a => a.AttributeType.FullName == "SNSRModLoader.SNSRModLoaderAttribute");
            var options = registration.Properties.ToDictionary(p => p.Name, p => p.Argument.Value);
            Check((bool)options["AutomaticUpdate"], "Automatic updates are not enabled.");
            var installed = new ModRequest {
                Id = (string)registration.ConstructorArguments[0].Value,
                Name = (string)registration.ConstructorArguments[1].Value,
                Version = new Version("0.0.0"), Hash = UpdatePackage.Hash(dll),
                FileName = Path.GetFileName(dll), Enabled = true, Selected = true,
                Repository = (string)options["UpdateRepository"], Manifest = (string)options["UpdateManifestUrl"]
            };
            var package = ModPackage.Parse(File.ReadAllBytes(Path.Combine(root, "dist", "mod-update.xml")), installed, "September2018");
            package.ValidateDll(dll, "September2018", loader);
            using (var sdk = AssemblyDefinition.ReadAssembly(Path.Combine(loader, "SNSRModLoader.dll")))
                Check(package.MinimumLoader >= sdk.Name.Version, "Manifest understates the required loader SDK.");
            var allowed = new HashSet<string> { "mscorlib", "System", "System.Core", "System.Xml", "SNSRModLoader", "0Harmony", "UnityEngine", "UnityEngine.UI", "Assembly-CSharp", "Assembly-CSharp-firstpass", "LitJson" };
            Check(assembly.MainModule.AssemblyReferences.All(a => allowed.Contains(a.Name)), "Release needs an unexpected auxiliary DLL.");
            foreach (var pair in new[] { ("RaceSeeds.csv", "Survival.csv"), ("CreativeRaceSeeds.csv", "Creative.csv"), ("HardcoreRaceSeeds.csv", "Hardcore.csv") })
            {
                var resource = (EmbeddedResource)assembly.MainModule.Resources.Single(r => r.Name == pair.Item1);
                Check(resource.GetResourceData().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Set Seed", "Seeds", pair.Item2))), "Embedded seed list is stale: " + pair.Item2);
            }
            Check(Directory.GetFiles(Path.Combine(root, "dist"), "*.dll").Length == 1, "Release contains extra DLLs.");
            Console.WriteLine("PASS: actual loader manifest/DLL validation; opt-in metadata; correct minimum SDK; all seeds embedded; single-DLL dependencies.");
        }
    }
}
