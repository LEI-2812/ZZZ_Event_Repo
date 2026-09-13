using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace BeastBeat.Editor
{
    [ScriptedImporter(1, "xlsx")]
    public sealed class GameWorkbookImporter : ScriptedImporter
    {
        static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = ScriptableObject.CreateInstance<GameWorkbook>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            try
            {
                byte[] bytes = File.ReadAllBytes(context.assetPath);
                using (var hash = SHA256.Create()) asset.revision = Convert.ToBase64String(hash.ComputeHash(bytes));
                asset.sheets = Read(bytes);
            }
            catch (Exception ex) { asset.importError = "XLSX 읽기 실패: " + ex.Message; asset.revision = Guid.NewGuid().ToString(); context.LogImportError(asset.importError); }
            context.AddObjectToAsset("GameWorkbook", asset);
            context.SetMainObject(asset);
        }
        public static GameWorkbook.Sheet[] Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var workbook = Xml(zip, "xl/workbook.xml");
                var relationships = Xml(zip, "xl/_rels/workbook.xml.rels");
                var targets = relationships.Root.Elements().ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
                var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() : Xml(zip, "xl/sharedStrings.xml").Root.Elements(Ns + "si").Select(Runs).ToArray();
                var dateStyles = new HashSet<int>();
                if (zip.GetEntry("xl/styles.xml") != null)
                {
                    var styles = Xml(zip, "xl/styles.xml");
                    var custom = styles.Descendants(Ns + "numFmt").ToDictionary(x => (int)x.Attribute("numFmtId"), x => (string)x.Attribute("formatCode"));
                    int index = 0;
                    foreach (var style in styles.Root.Element(Ns + "cellXfs").Elements(Ns + "xf"))
                    {
                        int id = (int?)style.Attribute("numFmtId") ?? 0;
                        bool isDate = id >= 14 && id <= 17 || id >= 27 && id <= 36 || id >= 50 && id <= 58;
                        if (custom.TryGetValue(id, out var format))
                        {
                            var plain = Regex.Replace(format, "\"[^\"]*\"|\\\\.|\\[[^\\]]*\\]", "").ToLowerInvariant();
                            isDate |= plain.Contains("y") || plain.Contains("d");
                        }
                        if (isDate) dateStyles.Add(index); index++;
                    }
                }
                string dateFlag = (string)workbook.Root.Element(Ns + "workbookPr")?.Attribute("date1904");
                bool date1904 = dateFlag == "1" || dateFlag == "true";
                var sheets = new List<GameWorkbook.Sheet>();
                foreach (var sheet in workbook.Descendants(Ns + "sheet"))
                {
                    string name = (string)sheet.Attribute("name");
                    string target = targets[(string)sheet.Attribute(Rel + "id")];
                    string path = new Uri(new Uri("http://xlsx.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                    var xml = Xml(zip, Uri.UnescapeDataString(path));
                    var records = new StringBuilder(); int width = 0;
                    foreach (var row in xml.Descendants(Ns + "sheetData").Elements(Ns + "row"))
                    {
                        int rowNumber = (int?)row.Attribute("r") ?? 0;
                        var values = new SortedDictionary<int, string>();
                        foreach (var cell in row.Elements(Ns + "c"))
                        {
                            string reference = (string)cell.Attribute("r");
                            if (cell.Element(Ns + "f") != null) throw new FormatException(name + "!" + reference + ": 수식 대신 값을 입력해 주세요.");
                            string type = (string)cell.Attribute("t");
                            string value = (string)cell.Element(Ns + "v") ?? "";
                            if (type == "s") value = shared[int.Parse(value, CultureInfo.InvariantCulture)];
                            else if (type == "inlineStr") value = Runs(cell.Element(Ns + "is"));
                            else if (type == "e") throw new FormatException(name + "!" + reference + ": " + value);
                            else if (type == "d") value = DateTime.Parse(value, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            else if ((type == null || type == "n") && dateStyles.Contains((int?)cell.Attribute("s") ?? 0) && value.Length > 0)
                            {
                                double serial = double.Parse(value, CultureInfo.InvariantCulture);
                                value = (date1904 ? new DateTime(1904, 1, 1).AddDays(serial) : DateTime.FromOADate(serial)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            }
                            if (value.Length > 0) values[Column(reference)] = value;
                        }
                        if (values.Count == 0) continue;
                        if (width == 0)
                        {
                            if (rowNumber != 1) throw new FormatException(name + ": 첫 행에 컬럼 이름을 입력해 주세요.");
                            width = values.Keys.Max() + 1;
                        }
                        if (values.Keys.Max() >= width) throw new FormatException(name + " " + rowNumber + "행: 헤더 밖에 데이터가 있습니다.");
                        for (int col = 0; col < width; col++)
                        {
                            if (col > 0) records.Append(',');
                            string value = values.TryGetValue(col, out var text) ? text : "";
                            records.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                        }
                        records.Append('\n');
                    }
                    sheets.Add(new GameWorkbook.Sheet { name = name, records = records.ToString() });
                }
                return sheets.ToArray();
            }
        }
        static string Runs(XElement element) => element == null ? "" : string.Concat(element.Descendants(Ns + "t").Where(t => !t.Ancestors(Ns + "rPh").Any()).Select(t => t.Value));
        static int Column(string reference)
        {
            int value = 0;
            foreach (char c in reference) { if (c < 'A' || c > 'Z') break; value = checked(value * 26 + c - 'A' + 1); }
            if (value == 0) throw new FormatException("잘못된 셀 주소: " + reference);
            return value - 1;
        }
        static XDocument Xml(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path) ?? throw new FormatException("XLSX 구성 파일 누락: " + path);
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit })) return XDocument.Load(reader);
        }
    }
}
