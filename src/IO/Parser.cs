using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UITS.Box.Collaborations.IO
{
    static internal class Parser
    {
        public static async Task<IEnumerable<string>> GetLoginsFromFile(string path)
        {
            if (!File.Exists(path)) throw new ArgumentException(String.Format("No file found at '{0}'", path), "path");

            string content;
            using (var streamReader = new StreamReader(path))
            {
                content = await streamReader.ReadToEndAsync();
            }

            if (String.IsNullOrWhiteSpace(content)) throw new ArgumentException(String.Format("File at '{0}' contains no content", path), "path");

            return content.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim());
        }
    }
}