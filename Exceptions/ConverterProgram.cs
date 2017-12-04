using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace Exceptions
{
	public class ConverterProgram
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public static void Main(params string[] args)
		{
		    var filenames = args.Any() ? args : new[] { "text.txt" };
		    Settings settings;
		    try
		    {
		        settings = LoadSettings();
		    }
		    catch (FileNotFoundException e)
		    {
		        log.Error(e, $" Файл настроек {Path.GetFileName(e.FileName)} отсутствует.");
                settings = Settings.Default;
            }
		    catch (InvalidOperationException exception)
		    {
		        if (exception.InnerException is XmlException x)
		            log.Error(x, $"Не удалось прочитать файл настроек \n{x}");
                return;
		    }
		    try
		    {
		        ConvertFiles(filenames, settings);
		    }
		    catch (Exception e)
		    {
		        log.Error(e);
		    }
		}

		private static void ConvertFiles(string[] filenames, Settings settings)
		{
			var tasks = filenames
				.Select(fn => Task.Run(() => ConvertFile(fn, settings)).ContinueWith(HandleExceptions))
				.ToArray();
			Task.WaitAll(tasks); 
		}

	    private static void HandleExceptions(Task task)
	    {
	        if (task.Exception == null) return;
	        foreach (var exception in task.Exception.InnerExceptions)
	            if (exception is FileNotFoundException e)
                    log.Error(e, $"Не удалось сконвертировать {Path.GetFileName(e.FileName)}\n{e}");
	    }

	    private static Settings LoadSettings() 
		{
			var serializer = new XmlSerializer(typeof(Settings));
			var content = File.ReadAllText("settings.xml");
			return (Settings) serializer.Deserialize(new StringReader(content));
		}

		private static void ConvertFile(string filename, Settings settings)
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
			if (settings.Verbose)
			{
				log.Info("Processing file " + filename);
				log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
			}
			IEnumerable<string> lines;
		    try
		    {
		        lines = PrepareLines(filename).Select(ConvertLine)
		            .Select(s => s.Length + " " + s).ToList();
		    }
		    catch (NullReferenceException e)
		    {
		        log.Error(e, $"Некорректная строка");
                return;
		    }
			catch (FileNotFoundException e)
			{
				log.Error(e, $"File {filename} not found"); 
				return;
			}
				
			File.WriteAllLines(filename + ".out", lines);
		}

		private static IEnumerable<string> PrepareLines(string filename)
		{
			var lineIndex = 0;
			foreach (var line in File.ReadLines(filename))
			{
				if (string.IsNullOrEmpty(line)) continue;
				yield return line.Trim();
				lineIndex++;
			}
			yield return lineIndex.ToString();
		}

	    public static string ConvertLine(string arg)
	    {
	        if (TryConvertAsDateTime(arg, out var result))
	            return result;
	        if (TryConvertAsDouble(arg, out result))
	            return result;
            return ConvertAsCharIndexInstruction(arg);
        }

		private static string ConvertAsCharIndexInstruction(string s)
		{
			var parts = s.Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2) return null;
			var charIndex = int.Parse(parts[0]);
			if ((charIndex < 0) || (charIndex >= parts[1].Length))
				return null;
			var text = parts[1];
			return text[charIndex].ToString();
		}

		private static bool TryConvertAsDateTime(string arg, out string result)
		{
		    if (DateTime.TryParse(arg, out var t))
		    {
		        result = t.ToString(CultureInfo.InvariantCulture);
		        return true;
		    }
		    result = null;
		    return false;
		}

		private static bool TryConvertAsDouble(string arg, out string result)
		{
		    if (double.TryParse(arg, out var t))
		    {
		        result = t.ToString(CultureInfo.InvariantCulture);
		        return true;
		    }
		    result = null;
		    return false;
		}
	}
}