using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace l2l_aggregator.Helpers.AggregationHelpers
{
    public enum RecognitionType
    {
        All,           // Всё вместе (по умолчанию)
        RussianText,
        EnglishText,
        Number
    }
    public class TemplateField : ObservableObject
    {
        private string _name = string.Empty;
        private string _type = string.Empty;
        private bool _isSelected = true;
        private XElement _element;
        private RecognitionType _recognition = RecognitionType.All; // добавлено

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public XElement Element
        {
            get => _element;
            set => SetProperty(ref _element, value);
        }

        public RecognitionType Recognition
        {
            get => _recognition;
            set => SetProperty(ref _recognition, value);
        }

        // Для привязки в ComboBox
        public static IEnumerable<RecognitionType> RecognitionTypes => Enum.GetValues(typeof(RecognitionType)).Cast<RecognitionType>();
    }
}
