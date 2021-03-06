
**Archived patch notes:**
  * v0.0.11-alpha (2015-01-23):
      * Updated to use .NET 4.5 - this version is now required to run T-SQL Flex.
	  * Improved scripter to continuously increment the "#Result" table number to prevent conflicts in a session.
      * Fixed lockup of SSMS when switching between database servers. (#33)
      * Fixed missing column header for anonymous columns in Excel export (#29)
	  * Added WEIGHT, TARGET, and NONE as T-SQL keywords.
      * Implemented improved logging.
  * v0.0.10-alpha (2014-09-11):
      * Implemented drag and drop of database objects from object explorer.
      * Fix for launching Excel exported files when there is a space in the profile path name.
      * Fix for scripting a field name that includes spaces without bracketizing it.
  * v0.0.9-alpha (2014-09-11):
      * Fix for "Invalid class string" exception in SSMS 2008 or lower (via pull request from David Pond - many thanks!)
      * Fix for "unable to determine identity of domain" isolated storage initialization bug in SSMS 2008 or lower.
      * Fixes for CTRL+A and CTRL+C - "Select all" and "copy" keyboard shortcuts now work as expected with T-SQL Flex.
      * Additional exception handling during data scripting - hopefully will allow more graceful recovery from out of memory exceptions, in particular.
      * Some clarifications to the installation instructions including mentioning that the user must unblock the DLLs.
      * Fixed the T-SQL Flex metadata so that the add-in info appears in the "SSMS Add-ins" dialog.
  * v0.0.8-alpha (2014-09-06):
      * Added about box with version info and links for feedback, issues, and updates.
	  * Added button to open the latest scripted XML spreadsheet in Excel.
	  * Fix for some rare cross-thread UI update issues.
  * v0.0.7-alpha (2014-09-02):
      * Significant bug fixes for internationalization issues surrounding time and number formatting including tests.  Cultures where . is used as the time separator and , as a decimal point should work OK now for both Excel and SQL INSERT scripts.  Special thanks to Gianluca Sartori (@SpaghettiDBA) for assistance with troubleshooting these issues.
	  * Bug fixes for incorrect columns and commas appearing in INSERT scripts due to hidden fields.
	  * Added more line-feeds to the scripted XML Spreadsheet 2003 output.
	  * Finished refactoring to "format" functions (this is an internal change only).
  * v0.0.6-alpha (2014-08-30):
      * Significant improvements to exception handling during all phases of querying and scripting
	  * Significant changes to disk-based buffering.  Now uses .NET IsolatedStorage.
	  * Significant refactoring - moved query processing logic from UI to Core DLL.
	  * Fixed bug where synthetic columns (select 'a' as [z]) were incorrectly hidden.
	  * Fixed bug with scripting binary and other byte[] data fields to Excel.
	  * Updated SIP Framework to 1.0.1.243 (from July 2014).
  * v0.0.5-alpha (2014-08-22):
      * Export to "XML Spreadsheet 2003" functionality added - this is very early alpha for this feature.
      * Started significant refactoring effort for data scripting in T-SQL field vs general presentation.
      * Started work to use a file stream rather than a string builder for scripting the data.  Currently only used with the Excel XML export.
  * v0.0.4-alpha (2014-06-18):
      * Converted to background worker.  Added cancel button, timer, and progress bar.
	  * Additional scripted output "minification" improvements (dropping insignificant decimals for example).
	  * Other improvements to quality of scripted output such as bracketing of keywords.
  * v0.0.3-alpha (2014-06-13): Fixed data script escaping bug for single quotes.
  * v0.0.2-alpha (2014-06-11): Data scripting implemented.  Improved window.
  * v0.0.1-alpha (2014-06-01): Initial release.  Schema scripting working.
