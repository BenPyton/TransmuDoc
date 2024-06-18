# TransmuDoc

This little command-line tool should be used with [my fork](https://github.com/BenPyton/KantanDocGen) of the great [KantanDocGen Plugin](https://github.com/kamrann/KantanDocGenPlugin).

This has been inspired by the original [KantanDocGen Tool](https://github.com/kamrann/KantanDocGenTool), it has been completely rewritten from scratch.\
Using the default xslt files will produce markdown files (instead of html in the original tool).\
However, TransmuDoc is able to transform any *.xml files (with no specific folder tree) in any output format.\
(*This tool uses an updated Saxon-HE version, allowing non-xml outputs*)

If the `-legacymode` argument is passed, then the tool will act like the original tool.\
Meaning that arguments `-indexxsl` `-classxsl` and `-nodexsl` can be passed to override the default xslt files used.

When in normal mode (no `-legacymode` argument), a generic xslt file is used (the default can be overriden with the `-xsltfile` argument) which is responsible to generate proper files depending on some values passed in the input files (by default it checks the `doctype` node to dispatch to the index, class or node xslt files).

Like in the original repo, it currently needs both `-fromintermediate` and `-intermediatedir` arguments.

### Example Usage

```
TransmuDoc.exe -outputdir="D:/UnrealProject/Build/GeneratedDoc" -fromintermediate -intermediatedir="D:/UnrealProject/Intermediate/KantanDocGen/DocName" -name=DocName -cleanoutput
```

### All Arguments:

Argument | Type | Default | Description
:-: | :-: | --- | ---
`name` | string | | **[Required]** The documentation name.
`basedir` | string | One folder up from the .exe | The base directory for the xslt files.<br/>
`outputdir` | string | The working directory from where the command has been called | The root directory where generated files will be saved.
`intermediatedir` | string | | The project's `Intermediate` folder.
`indexxsl` | string | `xslt/index_xform.xsl` | The xslt file to use for index page (relative to `basedir`).<br/>Only available if `legacymode` is present.
`classxsl` | string | `xslt/class_docs_xform.xsl` | The xslt file to use for class/enum/struct pages (relative to `basedir`).<br/>Only available if `legacymode` is present.
`nodexsl` | string | `xslt/node_docs_xform.xsl` | The xslt file to use for node pages (relative to `basedir`).<br/>Only available if `legacymode` is present.
`xslfile` | string | `xslt/generic_docs_xform.xsl` | The xslt file to use for all pages (relative to `basedir`).<br/>Only available if `legacymode` is \***not**\* present.
`extension` | string | `md` | The extension of the ouput files.
`fromintermediate` | bool | false | Tells if the doc should be generated from files in the project's `Intermediate` directory.<br/>*This is currently needed and will fail if not present!*
`cleanoutput` | bool | false | Delete the content of `outputdir` before generating new files.
`legacymode` | bool | false | Will use the `indexxsl`, `classxsl` and `nodexsl` like in the original tool, instead of the generic `xslfile`.
