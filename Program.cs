using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace DependencyFixup
{
    class Dependency
    {
        public string AssemblyName;
        public List<Dependency> Dependencies = new List<Dependency>();
        public List<Dependency> Parents = new List<Dependency>();

        public Dependency(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public override string ToString()
        {
            return AssemblyName + ", Dependency Count: " + Dependencies.Count;
        }
    }

    class Program
    {
        private string _basePath;
        private List<string> _foundAssemblies = new List<string>();
        List<Dependency> _dependencies = new List<Dependency>();
        List<Dependency> _flatList = new List<Dependency>();
        List<string> _assemblyFiles = new List<string>();
        
        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        private void Run(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("Usage: DependencyFixup [Path To Main Assembly] [Config File (wildcards ok)]");
                return;
            }

            if (Path.GetFileName(args[1]).EndsWith(".config", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                Console.WriteLine($"Config file pattern: {Path.GetFileName(args[1])} doesn't end with .config.");
                return;
            }

            _basePath = Path.GetDirectoryName(args[0]);

            var assemblyName = AssemblyName.GetAssemblyName(args[0]);

            var assembly = Assembly.LoadFrom(args[0]);

            ScanAssembly(assembly);

            _assemblyFiles.Sort();

            Console.WriteLine("References");
            foreach (var foundAssembly in _flatList.Where(p => _assemblyFiles.Contains(p.AssemblyName) == false && p.Parents.Count > 0).OrderBy(p => p.AssemblyName))
            {
                string closestFilename = _assemblyFiles.FirstOrDefault(p => p.Split(',').First().Equals(foundAssembly.AssemblyName.Split(',').First()));

                Console.WriteLine($"Transitive Dependency Requested:\t{GetShortName(foundAssembly.AssemblyName)}");
                Console.WriteLine($"Redirecting to:\t\t\t{GetShortName(closestFilename)}");
                Console.WriteLine($"Used By:");

                foreach (var parent in foundAssembly.Parents)
                    Console.WriteLine($"   {GetShortName(parent.AssemblyName)}");

                Console.WriteLine("\n");

                FixupConfigFiles(closestFilename, args[1]);
                
            }
        }

        private object GetShortName(string assemblyName)
        {
            // (?<name>.*?), Version=(?<version>.*?), Culture=(?<culture>.*?), PublicKeyToken=(?<token>.*?)$
            Regex assemblyNameFinder = new Regex(
                  "(?<name>.*?), Version=(?<version>.*?), Culture=(?<culture>.*?), PublicKeyToken=(?<token>.*?)$",
                RegexOptions.CultureInvariant
                | RegexOptions.Compiled
                );

            var match = assemblyNameFinder.Match(assemblyName);

            string name = match.Groups["name"].Value;
            string version = match.Groups["version"].Value;

            return $"{name}@{version}";
        }

        private void FixupConfigFiles(string redirectAssemblyName, string configFilePatten)
        {
            // (?<name>.*?), Version=(?<version>.*?), Culture=(?<culture>.*?), PublicKeyToken=(?<token>.*?)$
            Regex assemblyNameFinder = new Regex(
                  "(?<name>.*?), Version=(?<version>.*?), Culture=(?<culture>.*?), PublicKeyToken=(?<token>.*?)$",
                RegexOptions.CultureInvariant
                | RegexOptions.Compiled
                );

            var match = assemblyNameFinder.Match(redirectAssemblyName);

            string assemblyName = match.Groups["name"].Value;
            string version = match.Groups["version"].Value;
            string token = match.Groups["token"].Value;
            string culture = match.Groups["culture"].Value;

            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(configFilePatten), Path.GetFileName(configFilePatten)))
            {
                XmlDocument doc = new XmlDocument();
                
                doc.Load(file);

                XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("a", "urn:schemas-microsoft-com:asm.v1");

                var configurationNode = doc.SelectSingleNode("/configuration");

                if(configurationNode == null)
                {
                    Console.WriteLine($"Invalid config file: {file}");
                    continue;
                }

                var runtimeNode = doc.SelectSingleNode("/configuration/runtime");

                if (runtimeNode == null)
                    runtimeNode = configurationNode.AppendChild(doc.CreateElement("runtime"));

                var assemblyBindingNode = doc.SelectSingleNode("/configuration/runtime/a:assemblyBinding", ns);

                if(assemblyBindingNode == null)
                    assemblyBindingNode = runtimeNode.AppendChild(doc.CreateElement("assemblyBinding", "urn:schemas-microsoft-com:asm.v1"));

                var assemblyIdentityNode = doc.SelectSingleNode($"/configuration/runtime/a:assemblyBinding/a:dependentAssembly/a:assemblyIdentity[@name='{assemblyName}']", ns);
                XmlNode dependentAssemblyNode;

                if (assemblyIdentityNode == null)
                    dependentAssemblyNode = assemblyBindingNode.AppendChild(doc.CreateElement("dependentAssembly", "urn:schemas-microsoft-com:asm.v1"));
                else
                {
                    dependentAssemblyNode = assemblyIdentityNode.ParentNode;
                    dependentAssemblyNode.RemoveAll();
                }

                assemblyIdentityNode = dependentAssemblyNode.AppendChild(doc.CreateElement("assemblyIdentity", "urn:schemas-microsoft-com:asm.v1"));

                var nameAttribute = doc.CreateAttribute("name");
                nameAttribute.Value = assemblyName;
                assemblyIdentityNode.Attributes.Append(nameAttribute);

                var publicTokenAttribute = doc.CreateAttribute("publicKeyToken");
                publicTokenAttribute.Value = token;
                assemblyIdentityNode.Attributes.Append(publicTokenAttribute);

                var cultureAttribute = doc.CreateAttribute("culture");
                cultureAttribute.Value = culture;
                assemblyIdentityNode.Attributes.Append(cultureAttribute);

                var bindingRedirectNode = dependentAssemblyNode.AppendChild(doc.CreateElement("bindingRedirect", "urn:schemas-microsoft-com:asm.v1"));

                var oldVersionAttribute = doc.CreateAttribute("oldVersion");
                oldVersionAttribute.Value = $"0.0.0.0-{version}";
                bindingRedirectNode.Attributes.Append(oldVersionAttribute);

                var newVersionAttribute = doc.CreateAttribute("newVersion");
                newVersionAttribute.Value = version;
                bindingRedirectNode.Attributes.Append(newVersionAttribute);

                doc.Save(file);
            }
        }

        private void PrintAssemblyHeirarchy(Dependency foundAssembly, List<String> output)
        {
            output.Insert(0, foundAssembly.AssemblyName);

            for (int i = 0; i < output.Count; i++)
                output[i] = " " + output[i];

            foreach (var parent in foundAssembly.Parents)
                PrintAssemblyHeirarchy(parent, output);

            if (foundAssembly.Parents.Count == 0)
            {
                foreach(var line in output)
                    Console.WriteLine(line);
            }
        }

        private void ScanAssembly(Assembly assembly)
        {
            Dependency dependency = _flatList.Where(p => p.AssemblyName == assembly.FullName).FirstOrDefault();

            if (dependency == null)
            {
                dependency = new Dependency(assembly.FullName);
                _flatList.Add(dependency);
            }

            if (dependency.Dependencies.Count > 0)
                return;

            var references = assembly.GetReferencedAssemblies();

            foreach (var reference in references)
            {
                string filename = Path.Combine(_basePath, reference.Name.Split(',')[0] + ".dll".ToLower());

                if (File.Exists(filename) == true)
                {
                    var referencedDependency = _flatList.Where(p => p.AssemblyName == reference.FullName).FirstOrDefault();
                    if (referencedDependency == null)
                    {
                        referencedDependency = new Dependency(reference.FullName);
                        _flatList.Add(referencedDependency);
                    }

                    dependency.Dependencies.Add(referencedDependency);
                    referencedDependency.Parents.Add(dependency);


                    var referencedAssembly = Assembly.LoadFrom(filename);

                    if (_assemblyFiles.Contains(referencedAssembly.FullName) == false)
                    {
                        _assemblyFiles.Add(referencedAssembly.FullName);

                        ScanAssembly(referencedAssembly);
                    }
                }
            }
        }

    }
}
