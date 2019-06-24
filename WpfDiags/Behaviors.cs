using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KaosFormat;

namespace AppView
{
    public static class TagHelpDialogBehavior
    {
        public static readonly DependencyProperty TagHelpHitsProperty = DependencyProperty.RegisterAttached
        ("TagHelpHits", typeof (int), typeof (TagHelpDialogBehavior), new PropertyMetadata (default (int), OnTagHelpHitsChange));

        public static void SetTagHelpHits (DependencyObject source, int value)
         => source.SetValue (TagHelpHitsProperty, value);

        public static int GetTagHelpHits (DependencyObject source)
         => (int) source.GetValue (TagHelpHitsProperty);

        private static readonly string helpText = @"TRACKNUMBER
Required. Must be sequential and the order must match the order of tracks in the EAC log.

ALBUM
Required. Must be consistent for all tracks.

DATE
Required. Must be consistent for all tracks. Must be in the form YYYY or YYYY-MM-DD where YYYY between 1900 and 2099.

RELEASE DATE
Optional. If present, must be consistent for all tracks and in the form YYYY or YYYY-MM-DD where YYYY between 1900 and 2099.

ORIGINAL RELEASE DATE
Optional. If present, must be in the form YYYY or YYYY-MM-DD where YYYY between 1900 and 2099.

TRACKTOTAL
Optional. If present, must be digits only with no leading zeros, consistent for all tracks, and equal to the number of tracks in the EAC log.

DISCNUMBER, DISCTOTAL
Optional. If present, must be digits only with no leading zeros and consistent for all tracks.

ALBUMARTIST
Required if ARTIST tags are missing or not consistent. If present, must be consistent for all tracks.

ALBUMARTISTSORTORDER
Optional. May contain multiple entries. If present, all entries must be consistent for all tracks.

BARCODE, CATALOGNUMBER, COMPILATION, ORGANIZATION
Optional. If present, must be consistent for all tracks.

Notes:
1. Consistency is only tested when 'Match FLACs' is checked.
2. All tags are also checked for extra white space.
3. Checking 'Strict' will escalate severities.
";
        private static Popup dialog=null;
        private static void OnTagHelpHitsChange (DependencyObject dep, DependencyPropertyChangedEventArgs ea)
        {
            if (dialog == null)
                dialog = new Popup()
                {
                    Child = new TextBox() { TextWrapping=TextWrapping.Wrap, AcceptsReturn=true, IsReadOnly=true, Text=helpText },
                    Placement = PlacementMode.MousePoint, StaysOpen = false
                };
            dialog.IsOpen = true;
        }
    }

    public static class ShowContentsDialogBehavior
    {
        public static readonly DependencyProperty ShowContentsProperty = DependencyProperty.RegisterAttached
        ("ShowContents", typeof (FormatBase), typeof (ShowContentsDialogBehavior), new PropertyMetadata (default (FormatBase), OnShowContentsChange));

        public static void SetShowContents (DependencyObject source, FormatBase value)
         => source.SetValue (ShowContentsProperty, value);

        public static FormatBase GetShowContents (DependencyObject source)
         => (FormatBase) source.GetValue (ShowContentsProperty);

        private static void OnShowContentsChange (DependencyObject depy, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue is FormatBase fmt)
            {
                string text = fmt.ContentText;
                if (text != null)
                {
                    foreach (var owned in ((Window) depy).OwnedWindows)
                        if (owned is Window ownedWindow && Object.ReferenceEquals (fmt, ownedWindow.Tag))
                        {
                            ownedWindow.Activate();
                            return;
                        }

                    var contentDialog = new Window
                    {
                        Owner = (Window) depy,
                        Title = fmt.Name,
                        ShowInTaskbar = false,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Tag = fmt,
                        Content = new TextBox()
                        {
                            Text = text,
                            AcceptsReturn = true,
                            IsReadOnly = true,
                            MaxHeight=640,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                        }
                    };

                    contentDialog.Show();
                }
            }
        }
    }
}
