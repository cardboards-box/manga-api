This folder is included in the built binaries.
All of the scripts get run in the order their name specifies.
This is used to rebuild the database and progressively update the application at run time.

Ensure that dependent scripts are run before the script that depends on them by ensuring the file-names are ordered correctly.

Note: The `Procedures` folder aren't actually procedures... they're functions that emulate stored procedures from mssql
