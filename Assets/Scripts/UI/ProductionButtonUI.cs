using System;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ProductionButtonUI: MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Button _button;
        
        private ProductionItemData _item;
        private Action<ProductionItemData> _onClick;

        public void Initialize(ProductionItemData item, Action<ProductionItemData> onClick)
        {
            _item = item;
            _onClick = onClick;
            if (item.Icon != null)
            {
                _icon.sprite = item.Icon;
            }
           
            _nameText.text = item.ItemName;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(Click);
        }
        
        private void Click()
        {
            _onClick?.Invoke(_item);
        }
    }
}