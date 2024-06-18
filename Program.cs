// Copyright (c) 2024 Benoit Pelletier
// SPDX-License-Identifier: BSL-1.0
// Distributed under the Boost Software License, Version 1.0. 
// (See accompanying file LICENSE or copy at https://www.boost.org/LICENSE_1_0.txt)

using java.lang;
using org.xml.sax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace TransmuDoc
{
	// Holds arguments passed to the commandline call.
	internal class ConsoleArgs
	{
		[Required]
		public string Name;

		public string BaseDir;
		public string OutputDir;
		public string IntermediateDir;

		// xslt files for legacy mode
		public string IndexXsl;
		public string ClassXsl;
		public string NodeXsl;

		// xslt file for generic mode
		public string XslFile;

		// Output extension
		public string Extension;

		// flags
		public bool FromIntermediate = false;
		public bool CleanOutput = false;

		// If true, will act like the original KantanDocGen,
		// using the provided specific xslt files (index/class/node).
		// If false, will use a generic xslt file that is responsible to dispatch properly depending on the doctype node,
		// and also can dispatch file transformations in multiple threads.
		public bool LegacyMode = false;
	}

	internal class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("TransmuDoc called with arguments:");
			foreach(string arg in args)
			{
				Console.WriteLine(arg);
			}

			// Fill default values
			ConsoleArgs arguments = new ConsoleArgs();
			arguments.BaseDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..");
			arguments.OutputDir = Directory.GetCurrentDirectory();
			arguments.IndexXsl = "xslt/index_xform.xsl";
			arguments.ClassXsl = "xslt/class_docs_xform.xsl";
			arguments.NodeXsl = "xslt/node_docs_xform.xsl";
			arguments.XslFile = "xslt/generic_docs_xform.xsl";
			arguments.Extension = "md";

			// Override default values with the ones passed to commandline.
			ArgParser<ConsoleArgs> Parser = new ArgParser<ConsoleArgs>();
			if (!Parser.Parse(args, ref arguments))
			{
				Console.WriteLine($"Error: Failed to parse arguments.");
				return;
			}

			arguments.IndexXsl = Path.Combine(arguments.BaseDir, arguments.IndexXsl);
			arguments.ClassXsl = Path.Combine(arguments.BaseDir, arguments.ClassXsl);
			arguments.NodeXsl = Path.Combine(arguments.BaseDir, arguments.NodeXsl);
			arguments.XslFile = Path.Combine(arguments.BaseDir, arguments.XslFile);
			arguments.Extension = "." + arguments.Extension;

			// Doc generation without intermediate files currently not supported...
			if (!arguments.FromIntermediate)
			{
				Console.WriteLine($"Error: Missing 'fromintermediate' flag. Generating doc without intermediate files is currently not supported.");
				return;
			}

			if (!CheckDirectory(arguments.IntermediateDir))
			{
				Console.WriteLine($"Error: intermediate directory '{arguments.IntermediateDir}' does not exists.");
				return;
			}

			if (!CheckDirectory(arguments.BaseDir))
			{
				Console.WriteLine($"Error: base directory '{arguments.BaseDir}' does not exists.");
				return;
			}

			if (!CheckDirectory(arguments.OutputDir, create: true, clean: arguments.CleanOutput))
			{
				Console.WriteLine($"Error: output directory '{arguments.OutputDir}' does not exists.");
				return;
			}

			int success = 0, failed = 0;

			Action<string, bool> OnProcessEnd = (string file, bool result) =>
			{
				if (result)
				{
					Interlocked.Increment(ref success);
				}
				else
				{
					Console.WriteLine($"Error: Transformation failed for file {file} - skipping.");
					Interlocked.Increment(ref failed);
				}
			};

			Stopwatch sw = Stopwatch.StartNew();

			if (arguments.LegacyMode)
			{
				SaxonTransformation saxon = new SaxonTransformation();
				SaxonTransformer indexTransformer = saxon.CreateTransformer(arguments.IndexXsl);
				SaxonTransformer classTransformer = saxon.CreateTransformer(arguments.ClassXsl);
				SaxonTransformer nodeTransformer = saxon.CreateTransformer(arguments.NodeXsl);

				// Transform the index
				string InputIndexPath = Path.Combine(arguments.IntermediateDir, "index.xml");
				string OutputIndexPath = Path.Combine(arguments.OutputDir, "index" + arguments.Extension);
				OnProcessEnd(InputIndexPath, indexTransformer.TransformFile(InputIndexPath, OutputIndexPath));

				var SubFolders = Directory.EnumerateDirectories(arguments.IntermediateDir);
				foreach (string Sub in SubFolders)
				{
					string ClassTitle = Path.GetFileName(Sub);
					string OutputClassDir = Path.Combine(arguments.OutputDir, ClassTitle);
					CheckDirectory(OutputClassDir, create: true);

					string InputClassPath = Path.Combine(Sub, ClassTitle + ".xml");
					string OutputClassPath = Path.Combine(OutputClassDir, ClassTitle + arguments.Extension);
					OnProcessEnd(InputClassPath, classTransformer.TransformFile(InputClassPath, OutputClassPath));

					CopyDirectory(Path.Combine(Sub, "img"), Path.Combine(OutputClassDir, "img"));

					// Transform the nodes
					string NodeDir = Path.Combine(Sub, "nodes");
					if (CheckDirectory(NodeDir))
					{
						string OutputNodesDir = Path.Combine(OutputClassDir, "nodes");
						CheckDirectory(OutputNodesDir, create: true);
						IEnumerable<string> InputFiles = Directory.EnumerateFiles(NodeDir, "*.xml", SearchOption.TopDirectoryOnly);
						foreach (string InputPath in InputFiles)
						{
							string FileTitle = Path.GetFileNameWithoutExtension(InputPath);
							string OutputPath = Path.Combine(OutputNodesDir, FileTitle + arguments.Extension);
							OnProcessEnd(InputPath, nodeTransformer.TransformFile(InputPath, OutputPath));
						}
					}
				}
			}
			else
			{
				SaxonTransformation saxon = new SaxonTransformation();
				SaxonTransformer transformer = saxon.CreateTransformer(arguments.XslFile);

				string baseInputDir = Path.GetFullPath(arguments.IntermediateDir);
				string baseOutputDir = Path.GetFullPath(arguments.OutputDir);

				Action<string> ProcessFile = (xmlFile) =>
				{
					string inputFile = Path.GetFullPath(xmlFile);
					string fileName = Path.GetFileNameWithoutExtension(inputFile);
					string inputDir = Path.GetDirectoryName(inputFile);
					string relativeDir = inputDir.Replace(baseInputDir, string.Empty)
						.TrimStart(new char[] {
						Path.PathSeparator,
						Path.DirectorySeparatorChar,
						Path.AltDirectorySeparatorChar
						});
					string outputDir = Path.Combine(baseOutputDir, relativeDir);
					string outputFile = Path.Combine(outputDir, fileName + arguments.Extension);

					Directory.CreateDirectory(outputDir);
					CopyDirectory(Path.Combine(inputDir, "img"), Path.Combine(outputDir, "img"));
					OnProcessEnd(inputFile, transformer.TransformFile(inputFile, outputFile));
				};

				IEnumerable<string> xmlFiles = Directory.EnumerateFiles(arguments.IntermediateDir, "*.xml", SearchOption.AllDirectories);
				int total = xmlFiles.Count();
				if (transformer.CanBeThreaded)
				{
					xmlFiles.AsParallel().ForAll(xmlFile =>
					{
						ProcessFile(xmlFile);
					});
				}
				else
				{
					foreach (string xmlFile in xmlFiles)
					{
						ProcessFile(xmlFile);
					}
				}
			}

			sw.Stop();
			Console.WriteLine($"TransmuDoc completed in {sw.Elapsed.TotalSeconds}s : {success} succeeded | {failed} failed.");
		}

		private static bool CheckDirectory(string path, bool create = false, bool clean = false)
		{
			bool exists = Directory.Exists(path);
			if (exists && clean)
			{
				Directory.Delete(path, true);
				exists = false;
			}

			if (create && !exists)
			{
				Directory.CreateDirectory(path);
				exists = true;
			}

			return exists;
		}

		private static bool CopyDirectory(string source, string destination)
		{
			DirectoryInfo sourceInfo = new DirectoryInfo(source);
			DirectoryInfo destinationInfo = new DirectoryInfo(destination);

			if (!sourceInfo.Exists)
				return false;

			return RecursiveCopy(sourceInfo, destinationInfo);
		}

		private static bool RecursiveCopy(DirectoryInfo source, DirectoryInfo destination)
		{
			Directory.CreateDirectory(destination.FullName);

			foreach (FileInfo file in source.GetFiles())
			{
				file.CopyTo(Path.Combine(destination.FullName, file.Name), overwrite: true);
			}

			foreach (DirectoryInfo subdir in source.GetDirectories())
			{
				DirectoryInfo destSubdir = destination.CreateSubdirectory(subdir.Name);
				RecursiveCopy(subdir, destSubdir);
			}

			// TODO: what is success? failed if at least one was not copied?
			return true;
		}
	}
}
