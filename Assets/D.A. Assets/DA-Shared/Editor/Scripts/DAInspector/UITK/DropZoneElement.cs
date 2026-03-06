using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.DAI
{
    /// <summary>
    /// A drag-and-drop zone element for UI Toolkit with dashed border and plus icon.
    /// </summary>
    public class DropZoneElement : VisualElement
    {
        public event Action<string[]> OnFilesDropped;

        private readonly Label _iconLabel;
        private readonly Label _textLabel;
        private readonly Color _normalBorderColor;
        private readonly Color _hoverBorderColor;

        public DropZoneElement(string placeholderText = "Drop file here", string acceptedExtension = ".zip")
        {
            _normalBorderColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            _hoverBorderColor = new Color(0.3f, 0.7f, 1f, 1f);

            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.height = 80;
            style.marginTop = 5;
            style.marginBottom = 5;
            style.borderTopWidth = 2;
            style.borderBottomWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;
            style.borderTopColor = _normalBorderColor;
            style.borderBottomColor = _normalBorderColor;
            style.borderLeftColor = _normalBorderColor;
            style.borderRightColor = _normalBorderColor;
            style.borderTopLeftRadius = 8;
            style.borderTopRightRadius = 8;
            style.borderBottomLeftRadius = 8;
            style.borderBottomRightRadius = 8;

            // Dashed border simulation via background
            style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);

            // Plus icon
            _iconLabel = new Label("+")
            {
                style =
                {
                    fontSize = 32,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = _normalBorderColor,
                    marginBottom = 5
                }
            };
            Add(_iconLabel);

            // Placeholder text
            _textLabel = new Label(placeholderText)
            {
                style =
                {
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = _normalBorderColor
                }
            };
            Add(_textLabel);

            // Register drag events
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(e => OnDragPerform(e, acceptedExtension));
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            SetHighlight(true);
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            SetHighlight(false);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragPerform(DragPerformEvent evt, string acceptedExtension)
        {
            SetHighlight(false);

            string[] paths = DragAndDrop.paths;
            if (paths == null || paths.Length == 0)
                return;

            // Filter by extension if specified
            if (!string.IsNullOrEmpty(acceptedExtension))
            {
                var filteredPaths = new System.Collections.Generic.List<string>();
                foreach (string path in paths)
                {
                    if (path.EndsWith(acceptedExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        filteredPaths.Add(path);
                    }
                }
                paths = filteredPaths.ToArray();
            }

            if (paths.Length > 0)
            {
                DragAndDrop.AcceptDrag();
                OnFilesDropped?.Invoke(paths);
            }
        }

        private void SetHighlight(bool highlight)
        {
            Color color = highlight ? _hoverBorderColor : _normalBorderColor;
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
            _iconLabel.style.color = color;
            _textLabel.style.color = color;

            style.backgroundColor = highlight
                ? new Color(0.3f, 0.5f, 0.7f, 0.2f)
                : new Color(0.2f, 0.2f, 0.2f, 0.3f);
        }

        public void SetDroppedFile(string fileName)
        {
            _textLabel.text = fileName;
            _iconLabel.text = "✓";
            _iconLabel.style.color = new Color(0.3f, 0.8f, 0.3f, 1f);
        }

        public void Reset(string placeholderText = "Drop file here")
        {
            _textLabel.text = placeholderText;
            _iconLabel.text = "+";
            _iconLabel.style.color = _normalBorderColor;
        }
    }
}
