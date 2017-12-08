using Shaman.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Utf8;

namespace SqlDumpToCsv
{
    class Program
    {
        [Configuration(CommandLineAlias = "out-format")]
        public static OutFormat Configuration_OutFormat;
        [Configuration(CommandLineAlias = "table")]
        public static string Configuration_Table;
        [Configuration(CommandLineAlias = "table")]
        public static bool Configuration_AllTables;
        [Configuration(CommandLineAlias = "version")]
        public static bool Configuration_Version;

        static int Main(string[] args)
        {
            ConfigurationManager.Initialize(typeof(Program).Assembly, IsDebugBuild);

            try
            {
                return MainInternal();
            }
            catch (Exception ex)
            {
                var innermost = ex;
                while (innermost.InnerException != null) innermost = innermost.InnerException;
                Console.Error.WriteLine(innermost);
                return 1;
            }
        }

        private static int MainInternal()
        {
            if (Configuration_Version)
            {
                Console.WriteLine(@"sqldump-to-csv " + ConfigurationManager.GetInformationalVersion(typeof(Program).Assembly));
                return 0;
            }

            if (IsHelpRequested(true))
            {
                Console.WriteLine(
@"sqldump-to-csv "+ConfigurationManager.GetInformationalVersion(typeof(Program).Assembly)+@"
github.com/antiufo/sqldump-to-csv

Usage: 
  sqldump-to-csv <sql-dump> [out-file] [--out-format csv|json|xlsx] [--table name]

sql-dump        Path to a .sql or .sql.gz file
out-file        Path to a destination .csv, .json or .xlsx file (or folder)
--out-format    Specifies an out format
--table         Name of the table to export
--all-tables    Exports all the tables, each one to a separate file
");
                return 0;
            }

            var positional = ConfigurationManager.PositionalCommandLineArgs.ToList();
            if (positional.Count == 0)
            {
                Console.Error.WriteLine("Please specify an input sql dump. See --help.");
                return 1;
            }

            var sqldump = positional[0];
            if (!File.Exists(sqldump))
            {
                Console.Error.WriteLine($"Cannot find '{sqldump}'.");
                return 1;
            }
            var outfile = positional.ElementAtOrDefault(1);
            if (Configuration_AllTables && Configuration_Table != null)
            {
                Console.Error.WriteLine("Cannot specify both --table and --all-tables.");
                return 1;
            }

            var outformat = Configuration_OutFormat;
            if (outformat == OutFormat.Unspecified && outfile != null)
            {
                var ext = Path.GetExtension(outfile).ToLowerInvariant();
                if (!string.IsNullOrEmpty(ext))
                    ext = ext.Substring(1);
                Enum.TryParse<OutFormat>(ext, out outformat);
            }

            if (outformat == OutFormat.Unspecified && outfile != null)
            {
                Console.Error.WriteLine("Cannot determine an output format based on the file extension. Please specify --out-format.");
                return 1;
            }

            using (var inputstream = File.Open(sqldump, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                Stream sqlstream = inputstream;
                if (Path.GetExtension(sqldump).Equals(".gz", StringComparison.InvariantCultureIgnoreCase))
                {
                    sqlstream = new GZipStream(inputstream, System.IO.Compression.CompressionMode.Decompress);
                }


                var tables = new List<TableInfo>();
                var currentTableName = Utf8String.Empty;
                TableInfo currentTable = null;
                bool hasFoundTable = false;

                var outputSummary = outfile == null && Configuration_Table == null && !Configuration_AllTables;
                OutputEmitter summaryEmitter = outputSummary ? new OutputEmitter<TableInfo>(outfile) : null;


                
                var tableEmitters = new Dictionary<string, OutputEmitter>();
                OutputEmitter currentTableEmitter = null;

                using (var sql = new SqlDumpReader(sqlstream))
                {
                    while (true)
                    {
                        var row = sql.TryReadRow();
                        if (row.Length == 0) break;
                        if (sql.CurrentTableName != currentTableName)
                        {
                            var table = new TableInfo();

                            table.Name = sql.CurrentTableName.ToString();
                            table.Columns = sql.CurrentTableColumnNames.Select(x => x.ToString()).ToArray();
                            tables.Add(table);
                            currentTable = table;
                            currentTableName = sql.CurrentTableName;
                            tableEmitters.TryGetValue(table.Name, out currentTableEmitter);
                            if (currentTableEmitter == null)
                            {
                                var elementType = ReplExtensions.CreateAnonymousType(table.Columns);
                                currentTableEmitter = OutputEmitter.Create(outfile, elementType);
                                tableEmitters[table.Name] = currentTableEmitter;
                            }
 
                        }
                        currentTable.RowCount++;
                    }
                }

                if (outputSummary)
                {
                    tables.ForEach(x => summaryEmitter.Emit(x));
                    summaryEmitter.Dispose();
                }

                foreach (var table in tables)
                {
                    if (tableEmitters.TryGetValue(table.Name, out var k))
                        k.Dispose();
                }


                if (Configuration_Table != null && !hasFoundTable)
                {
                    Console.Error.WriteLine($"Unable to find table {Configuration_Table}. Use sqldump-to-csv \"{sqldump}\" (with no other args) to see a list of tables that were found.");
                    return 1;
                }

                if (tables.Count == 0)
                {
                    Console.Error.WriteLine("No tables were found.");
                    return 1;
                }


            }
            
            return 0;
        }


        private static bool IsHelpRequested(bool alsoWithNoParameters)
        {
            var z = ConfigurationManager.GetCommandLineArgs();
            if (alsoWithNoParameters && z.Length <= 1) return true;
            return new[] { "/?", "/help", "-h", "-help", "--help" }.Intersect(z).Any();
            
        }

#if DEBUG
        private readonly static bool IsDebugBuild = true;
#else
        private readonly static bool IsDebug = false;
#endif
    }

    internal class TableInfo
    {
        public string Name;
        public int RowCount;

        public string[] Columns;
    }

    public enum OutFormat
    {
        Unspecified,
        Csv,
        Json,
        Xlsx
    }
}
