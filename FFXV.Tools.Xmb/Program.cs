﻿using FFXV.Services;
using FFXV.Utilities;
using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FFXV.Tools.XmbTool
{
	[Command(Description = "Manage Luminous Engine's XMB files, which have EBEX as extension")]
	class Program
	{
		private static readonly string ExtXml = ".xml";
		private static readonly string ExtExml = ".exml";
		private const bool IsParallel = true;

		private delegate void Operation(string input, string output);

		public static int Main(string[] args)
		{
			CommandLineApplication.Execute<Program>(args);
			return 0;
		}


		[Option("-x|--export", "Specify if the input is the one that has to be imported", CommandOptionType.NoValue, LongName = "--export")]
		public bool IsExport { get; }

		[Option("-i|--input", "Input file or folder", CommandOptionType.SingleValue, LongName = "input")]
		[Required]
		public string Input { get; }

		[Option("-o|--output", "Output file or folder", CommandOptionType.SingleValue, LongName = "output")]
		[Required]
		public string Output { get; }

		[Option("-v|--verbose", "Print all the information", CommandOptionType.NoValue, LongName = "verbose")]
		public bool IsVerbose { get; }

		[Option("-d|--directory", "Process a directory instead of a file", CommandOptionType.NoValue, LongName = "directory")]
		public bool IsDirectory { get; }

		[Option("-r|--recursive", "Recursively process all the sub-directories", CommandOptionType.NoValue, LongName = "recursive")]
		public bool IsRecursive { get; }

		private void OnExecute()
		{
			if (IsDirectory)
			{
				if (IsExport)
					Export(Input);
				else
					Import(Input);
			}
			else
			{
				if (IsExport)
					Export(Input, Output);
				else
					Import(Input, Output);
			}
		}

		private int MaxParallelTasksCount = Process.GetCurrentProcess().Threads.Count;

		private static bool IsExml(string fileName) => fileName.EndsWith(ExtExml);
		private static bool IsXml(string fileName) => fileName.EndsWith(ExtXml);

		private void Export(string path)
		{
			var fileNames = GetFilesList(new List<string>(), path, IsRecursive, IsExml);

			if (IsParallel)
			{
				var tasks = fileNames.Select(fileName => Task.Run(() =>
				{
					Do(Export, fileName, fileName.Replace(ExtExml, ExtXml));
				}));

				tasks.WaitAll(MaxParallelTasksCount);
			}
			else
			{
				foreach (var fileName in fileNames)
				{
					Do(Export, fileName, fileName.Replace(ExtExml, ExtXml));
				}
			}
		}

		private void Export(string input, string output)
		{
			if (IsVerbose)
				Console.WriteLine(input);

			XDocument doc;
			using (var stream = File.OpenRead(input))
			{
				doc = new Xmb(new BinaryReader(stream)).Document;
			}

			using (var stream = File.Create(output))
			{
				doc.Save(stream);
			}
		}

		private void Import(string path)
		{
			var fileNames = GetFilesList(new List<string>(), path, IsRecursive, IsXml);

			if (IsParallel)
			{
				var tasks = fileNames.Select(fileName => Task.Run(() =>
				{
					Do(Import, fileName, fileName.Replace(ExtXml, ExtExml));
				}));

				tasks.WaitAll(MaxParallelTasksCount);
			}
			else
			{
				foreach (var fileName in fileNames)
				{
					Do(Import, fileName, fileName.Replace(ExtXml, ExtExml));
				}
			}
		}

		private void Import(string input, string output)
		{
			if (IsVerbose)
				Console.WriteLine(input);

			XDocument doc;
			using (var stream = File.Open(input, FileMode.Open))
			{
				doc = XDocument.Load(stream);
			}

			using (var stream = File.Open(output, FileMode.Create))
			{
				new Xmb(doc).Write(new BinaryWriter(stream));
			}
		}

		private void Do(Operation operation, string input, string output)
		{
			if (IsVerbose)
				Console.WriteLine(input);

			try
			{
				operation(input, output);
			}
			catch (Exception ex)
			{
				var prevColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"{input}: {ex.Message}");
				Console.ForegroundColor = prevColor;
			}
		}

		private List<string> GetFilesList(List<string> list, string path, bool includeSubDirectories, Func<string, bool> expression)
		{
			foreach (var file in Directory.GetFiles(path))
			{
				if (expression(file))
				{
					list.Add(file);
				}
			}

			if (includeSubDirectories)
			{
				foreach (var dir in Directory.GetDirectories(path))
				{
					GetFilesList(list, dir, includeSubDirectories, expression);
				}
			}

			return list;
		}
	}
}
