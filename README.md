# sqldump-to-csv

Converts SQL dumps to CSV, JSON or XLSX.

## Usage

`sqldump-to-csv <sql-dump> [out-file] [--out-format csv|json|xlsx] [--table name]`

* `<sql-dump>` Path to a `.sql` or `.sql.gz` file
* `<out-file>` Path to a destination `.csv`, `.json` or `.xlsx` file (or folder)
* `--out-format <format>` Specifies an out format (`csv`, `json` or `xlsx`)
* `--table <name>` Name of the table to export
* `--all-tables` Exports all the tables, each one to a separate file

If you only specify the input file, a summary of the tables in the file will be shown instead.

## Examples
* `sqldump-to-csv Northwind.sql`
* `sqldump-to-csv Northwind.sql exported-customers.csv --table Customers`
* `sqldump-to-csv Northwind.sql exported\ --all-tables --out-format csv`

