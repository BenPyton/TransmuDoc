// Copyright (c) 2024 Benoit Pelletier
// SPDX-License-Identifier: BSL-1.0
// Distributed under the Boost Software License, Version 1.0. 
// (See accompanying file LICENSE or copy at https://www.boost.org/LICENSE_1_0.txt)

using Saxon.Api;
using System;
using System.IO;

namespace TransmuDoc
{
	public class SaxonTransformation : IFileTransformation<SaxonTransformer>
	{
		public SaxonTransformation()
		{
			processor = new Processor();
			compiler = processor.NewXsltCompiler();
		}

		public SaxonTransformer CreateTransformer(string xsltFile)
		{
			XsltExecutable executable = compiler.Compile(new Uri(xsltFile));
			return new SaxonTransformer(executable);
		}

		private Processor processor = null;
		private XsltCompiler compiler = null;
	}

	public class SaxonTransformer : IFileTransformer
	{
		public bool CanBeThreaded {  get { return true; } }

		internal SaxonTransformer(XsltExecutable exe)
		{
			if (exe == null)
				throw new ArgumentNullException("exe");
			executable = exe;
		}

		public bool TransformFile(string inputFile, string outputFile)
		{
			bool success = true;
			try
			{
				RawDestination destination = new RawDestination();
				using (FileStream inputStream = File.OpenRead(inputFile))
				{
					XsltTransformer transformer = executable.Load();
					using (MessageListener msg = new MessageListener(transformer))
					{
						msg.OnError = () => { success = false; };
						transformer.BaseOutputUri = new Uri(outputFile);
                        transformer.SetInputStream(inputStream, new Uri(inputFile));
						transformer.Run(destination); // this will set HasError if an xsl:message contains "error:"
					}
				}

				using (StreamWriter outputStream = File.CreateText(outputFile))
				{
					foreach (XdmItem item in destination.XdmValue)
					{
						outputStream.Write(item.GetStringValue());
					}
					outputStream.Flush();
				}
			}
			catch (Exception Ex)
			{
				Console.WriteLine(Ex.ToString() + "\n" + Ex.StackTrace);
				success = false;
			}

			//Console.WriteLine("File transformation {1}: {0}", inputFile, success ? "succeeded" : "failed");
			return success;
		}

		internal class MessageListener : IMessageListener2, IDisposable
		{
			public delegate void ErrorDelegate();

			public ErrorDelegate OnError { get; set; } = null;

			public MessageListener(XsltTransformer transformer)
			{
				this.transformer = transformer;
				if (transformer.MessageListener2 != null)
					throw new Exception("Can't bind MessageListener: XsltTransformer has already a MessageListener bound to it.");
				transformer.MessageListener2 = this;
			}

			public void Message(XdmNode content, QName errorCode, bool terminate, IXmlLocation location)
			{
				string message = content.ToString();
				Console.WriteLine(message);
				if (message.ToString().ToLower().Contains("error:"))
					OnError?.Invoke();
			}

			public void Dispose()
			{
				if (transformer.MessageListener2 != this)
					throw new Exception("Can't dispose MessageListener: XsltTransformer has another MessageListener bound to it.");
				transformer.MessageListener2 = null;
			}

			private XsltTransformer transformer = null;
		}

		private XsltExecutable executable = null;
	}
}
