[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments=$true)][object[]]$LegacyArguments
)
throw 'The isolated wardrobe proof runner is retired. No Unity process was started. Source preparation uses scripts/import-lara-outfit.ps1; full main-editor conversion is deferred. The original runner and procedure are preserved in docs/archive/p02-import-evidence/workflow-source/scripts/. Review existing outfits through the saved Clothing Setup scene.'
