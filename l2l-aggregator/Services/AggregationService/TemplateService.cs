using l2l_aggregator.Helpers.AggregationHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace l2l_aggregator.Services.AggregationService
{
    public class TemplateService
    {
        public XDocument OriginalDocument { get; private set; }

        public List<TemplateField> LoadTemplate(byte[] template)
        {
            var fields = new List<TemplateField>();

            if (template == null || template.Length == 0)
                return fields;

            try
            {
                //byte[] templateBytes = Convert.FromBase64String(template);
                using (var memoryStream = new MemoryStream(template))
                {
                    OriginalDocument = XDocument.Load(memoryStream);
                }
                //TfrxReportPage новый, TfrxTemplatePage старый
                var templatePage = OriginalDocument.Descendants()
                                   .FirstOrDefault(e => (
                        e.Name.LocalName == "TfrxReportPage" ||
                        e.Name.LocalName == "TfrxTemplatePage"
                        ));

                if (templatePage == null)
                    return fields;

                foreach (var element in templatePage.Elements())
                {
                    var nameAttr = element.Attribute("Name");
                    var textAttr = element.Attribute("Text");
                    var dataFieldAttr = element.Attribute("DataField");
                    var expressionAttr = element.Attribute("Expression");

                    if (nameAttr != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dataFieldAttr?.Value))
                        {
                            fields.Add(new TemplateField
                            {
                                Name = dataFieldAttr.Value,
                                Type = "переменная",
                                Element = element,
                                IsSelected = true
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(expressionAttr?.Value))
                        {
                            fields.Add(new TemplateField
                            {
                                Name = ExtractFieldName(expressionAttr.Value),
                                Type = "переменная",
                                Element = element,
                                IsSelected = true
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(textAttr?.Value) && textAttr.Value.StartsWith("["))
                        {
                            // Значение в [] — вероятно, выражение
                            fields.Add(new TemplateField
                            {
                                Name = ExtractFieldName(textAttr.Value),
                                Type = "переменная",
                                Element = element,
                                IsSelected = true
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(textAttr?.Value))
                        {
                            fields.Add(new TemplateField
                            {
                                Name = textAttr.Value,
                                Type = "текст",
                                Element = element,
                                IsSelected = true
                            });
                        }
                    }
                }

                return fields;
            }
            catch
            {
                return new List<TemplateField>();
            }
        }
        private string ExtractFieldName(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return string.Empty;

            // Пример выражения: [LabelQry."UN_CODE"] или <LabelQry."UN_CODE">
            var regex = new System.Text.RegularExpressions.Regex(@"[\[\<]\s*(\w+)\.\s*""?(\w+)""?\s*[\]\>]");
            var match = regex.Match(expression);

            if (match.Success)
                return match.Groups[2].Value;

            return expression; // fallback
        }
        public string GenerateTemplate(List<TemplateField> templateFields)
        {
            if (OriginalDocument == null || templateFields.Count == 0)
                return string.Empty;

            var newDocument = new XDocument(OriginalDocument);

            // Очистка ненужного
            newDocument.Descendants("Datasets").Remove();
            newDocument.Descendants("Variables").Remove();

            var dataPage = newDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == "TfrxDataPage");
            if (dataPage != null)
            {
                var allowedAttrs = new HashSet<string> { "Name", "Height", "Left", "Top", "Width" };
                foreach (var attr in dataPage.Attributes().Where(a => !allowedAttrs.Contains(a.Name.LocalName)).ToList())
                {
                    attr.Remove();
                }
            }
            //TfrxReportPage новый, TfrxTemplatePage старый
            var templatePage = newDocument.Descendants().FirstOrDefault(e => (
                        e.Name.LocalName == "TfrxReportPage" ||
                        e.Name.LocalName == "TfrxTemplatePage"
                        ));
            if (templatePage == null)
                return string.Empty;

            // Очистка атрибутов страницы
            foreach (var attr in templatePage.Attributes().Where(
                a => string.IsNullOrWhiteSpace(a.Value) || a.Value == "0").ToList())
            {
                attr.Remove();
            }

            // Удаление невыбранных полей
            var fieldsToRemove = new List<XElement>();
            foreach (var element in templatePage.Elements())
            {
                var field = templateFields.FirstOrDefault(f => f.Element.Attribute("Name")?.Value == element.Attribute("Name")?.Value);
                if (field != null && !field.IsSelected)
                {
                    fieldsToRemove.Add(element);
                }
                else if (field != null)
                {
                    var allowedAttrs = new HashSet<string>
                    {
                        "Name", "Left", "Top", "Width", "Height", "Font.Name", "Text", "DataField"
                    };

                    foreach (var attr in element.Attributes().Where(a => !allowedAttrs.Contains(a.Name.LocalName)).ToList())
                    {
                        attr.Remove();
                    }
                    element.SetAttributeValue("RecognitionType", field.Recognition.ToString()); // добавление атрибута распознавания

                    // Установка Type и Name
                    element.SetAttributeValue("Type", field.Type == "переменная" ? "variable" : "text");
                    if (field.Type == "переменная")
                    {
                        // Установка Name как имя переменной
                        element.SetAttributeValue("Name", field.Name);
                    }
                    else if (field.Type == "текст")
                    {
                        // Установка Name как текстовое содержимое
                        var textAttr = element.Attribute("Text");
                        if (textAttr != null)
                        {
                            element.SetAttributeValue("Name", textAttr.Value);
                        }
                    }
                }
            }

            foreach (var el in fieldsToRemove)
            {
                el.Remove();
            }

            using (var stringWriter = new Utf8StringWriter())
            {
                newDocument.Save(stringWriter, SaveOptions.None);
                return stringWriter.ToString();
            }
        }
        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
        private string EncodeToBase64(XDocument doc)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                doc.Save(ms);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}
