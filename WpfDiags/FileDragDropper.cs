using System;
using System.Windows;
using System.Windows.Controls;

namespace AppViewModel
{
    public class FileDragDropper
    {
        public static bool GetIsFileDragDropEnabled (DependencyObject obj)
         => (bool) obj.GetValue (IsFileDragDropEnabledProperty);

        public static void SetIsFileDragDropEnabled (DependencyObject obj, bool value)
         => obj.SetValue (IsFileDragDropEnabledProperty, value);

        public static bool GetFileDragDropTarget (DependencyObject obj)
         => (bool) obj.GetValue (FileDragDropTargetProperty);

        public static void SetFileDragDropTarget (DependencyObject obj, bool value)
         => obj.SetValue (FileDragDropTargetProperty, value);

        public static readonly DependencyProperty IsFileDragDropEnabledProperty =
            DependencyProperty.RegisterAttached ("IsFileDragDropEnabled", typeof (bool), typeof (FileDragDropper),
                                                 new PropertyMetadata (OnFileDragDropEnabled));

        public static readonly DependencyProperty FileDragDropTargetProperty =
            DependencyProperty.RegisterAttached ("FileDragDropTarget", typeof (object), typeof (FileDragDropper), null);

        private static void OnFileDragDropEnabled (DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != e.OldValue && obj is Control control)
                control.Drop += OnDrop;
        }

        private static void OnDrop (object sender, DragEventArgs args)
        {
            if (! (sender is DependencyObject obj))
                return;
            Object target = obj.GetValue (FileDragDropTargetProperty);
            if (! (target is IFileDragDropTarget fileTarget))
                throw new ArgumentException ("FileDragDropTarget object must be of type " + nameof (IFileDragDropTarget), nameof (sender));
            else if (args.Data.GetDataPresent (DataFormats.FileDrop))
                fileTarget.OnFileDrop ((string[]) args.Data.GetData (DataFormats.FileDrop));
        }
    }
}
