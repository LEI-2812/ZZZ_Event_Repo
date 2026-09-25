using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace BeastBeat
{
    // XLSX의 users 시트에서 해금 셀만 바꿉니다. 다른 시트/서식/표는 그대로 복사합니다.
    public static class WorkbookOwnershipStore
    {
        static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void SaveUnlocked(string path, int userId, IEnumerable<int> bangbooIds)
        {
            var ids = bangbooIds.Distinct().ToArray();
            if (ids.Length == 0) return;
            string temporary = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                // 다른 쓰기를 차단한 채 최신 파일을 읽습니다. Excel에서 편집 중이면 실패하고 재시도합니다.
                using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete))
                using (var original = new MemoryStream())
                {
                    file.CopyTo(original); original.Position = 0;
                    using (var zip = new ZipArchive(original, ZipArchiveMode.Read, true))
                    {
                        var workbook = ReadXml(zip, "xl/workbook.xml");
                        var sheets = workbook.Descendants(Ns + "sheet").Where(s => (string)s.Attribute("name") == "users").ToArray();
                        if (sheets.Length != 1) throw new FormatException("users 시트가 없거나 중복되었습니다.");
                        var relationships = ReadXml(zip, "xl/_rels/workbook.xml.rels");
                        string relation = (string)sheets[0].Attribute(Rel + "id");
                        var target = relationships.Root.Elements().Single(e => (string)e.Attribute("Id") == relation);
                        string sheetPath = Uri.UnescapeDataString(new Uri(new Uri("http://xlsx.local/xl/workbook.xml"), (string)target.Attribute("Target")).AbsolutePath.TrimStart('/'));
                        string[] shared = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() :
                            ReadXml(zip, "xl/sharedStrings.xml").Root.Elements(Ns + "si").Select(Text).ToArray();
                        var sheet = ReadXml(zip, sheetPath);
                        var rows = sheet.Descendants(Ns + "sheetData").Elements(Ns + "row").ToArray();
                        var header = rows.Single(r => (string)r.Attribute("r") == "1");
                        var columns = header.Elements(Ns + "c").ToDictionary(c => Value(c, shared).Trim(), c => Column((string)c.Attribute("r")));
                        if (!columns.TryGetValue("id", out var idColumn)) throw new FormatException("users.id 컬럼 누락");
                        var users = rows.Skip(1).Where(r => r.Elements(Ns + "c").Any(c => Column((string)c.Attribute("r")) == idColumn && Value(c, shared) == userId.ToString())).ToArray();
                        if (users.Length != 1) throw new FormatException("users: 현재 사용자 id=" + userId + "가 없거나 중복되었습니다.");
                        bool changed = false;
                        foreach (int id in ids)
                        {
                            string key = "has_bangboo_" + id;
                            if (!columns.TryGetValue(key, out var column)) throw new FormatException("users." + key + " 컬럼 누락");
                            string address = column + (string)users[0].Attribute("r");
                            var cell = users[0].Elements(Ns + "c").SingleOrDefault(c => (string)c.Attribute("r") == address);
                            if (cell == null) throw new FormatException("users!" + address + " 보유 셀 누락");
                            string value = Value(cell, shared);
                            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
                            if (cell.Element(Ns + "f") != null) throw new FormatException("users!" + address + " 수식 셀은 수정할 수 없습니다.");
                            cell.Elements(Ns + "v").Remove(); cell.Elements(Ns + "is").Remove();
                            cell.SetAttributeValue("t", "b"); cell.AddFirst(new XElement(Ns + "v", "1"));
                            changed = true;
                        }
                        if (!changed) return;
                        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (var result = new ZipArchive(output, ZipArchiveMode.Create))
                        {
                            foreach (var entry in zip.Entries)
                            {
                                var copy = result.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                                copy.LastWriteTime = entry.LastWriteTime;
                                using (var destination = copy.Open())
                                {
                                    if (entry.FullName == sheetPath) sheet.Save(destination, SaveOptions.DisableFormatting);
                                    else using (var source = entry.Open()) source.CopyTo(destination);
                                }
                            }
                        }
                        // 완성된 파일만 원본과 교체합니다. 중간 오류로 XLSX가 일부만 저장되는 일을 막습니다.
                        File.Replace(temporary, path, null);
                    }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static string Column(string address) => new string(address.TakeWhile(char.IsLetter).ToArray());
        static string Text(XElement element) => element == null ? "" : string.Concat(element.Descendants(Ns + "t").Where(t => !t.Ancestors(Ns + "rPh").Any()).Select(t => t.Value));
        static string Value(XElement cell, string[] shared)
        {
            string value = (string)cell.Element(Ns + "v") ?? "";
            switch ((string)cell.Attribute("t")) { case "s": return shared[int.Parse(value)]; case "inlineStr": return Text(cell); default: return value; }
        }
        static XDocument ReadXml(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path) ?? throw new FormatException("XLSX 구성 파일 누락: " + path);
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
    }
}
