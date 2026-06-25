using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace NLog.Targets.NetworkJSON.Tests.Resources
{
    internal class ResourceHelper
    {
        internal static TextReader GetResource(string filename)
        {
            Assert.That(filename, Is.Not.Null);
            var thisAssembly = Assembly.GetExecutingAssembly();
            var resourceFullName = typeof (ResourceHelper).Namespace + "." + filename;
            var manifestResourceStream = thisAssembly.GetManifestResourceStream(resourceFullName);
            Assert.That(manifestResourceStream, Is.Not.Null, "Resource not found in this assembly: " + resourceFullName);

            return new StreamReader(manifestResourceStream);
        }
    }
}
