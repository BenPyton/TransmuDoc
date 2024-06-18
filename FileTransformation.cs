// Copyright (c) 2024 Benoit Pelletier
// SPDX-License-Identifier: BSL-1.0
// Distributed under the Boost Software License, Version 1.0. 
// (See accompanying file LICENSE or copy at https://www.boost.org/LICENSE_1_0.txt)

namespace TransmuDoc
{
	public interface IFileTransformation<T>
		where T : IFileTransformer
	{
		T CreateTransformer(string xsltFile);
	}

	public interface IFileTransformer
	{
		bool CanBeThreaded { get; }
		bool TransformFile(string inputFile, string outputDestination);
	}
}
