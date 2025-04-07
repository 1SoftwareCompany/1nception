﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace One.Inception;

internal static class FileSearchExtensions
{
    public static IEnumerable<string> GetFiles(this string path, string regexPattern = "", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        System.Text.RegularExpressions.Regex reSearchPattern = new System.Text.RegularExpressions.Regex(regexPattern);
        return Directory.EnumerateFiles(path, "*", searchOption).Where(file => reSearchPattern.IsMatch(Path.GetFileName(file)));
    }

    public static IEnumerable<string> GetFiles(this string path, string[] searchPatterns, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return searchPatterns.AsParallel().SelectMany(searchPattern => Directory.EnumerateFiles(path, searchPattern, searchOption));
    }
}
