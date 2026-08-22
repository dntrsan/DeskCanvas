// The single global-using file for DeskCanvas.App.
//
// WPF and WinForms are both enabled (the tray icon needs WinForms), so the SDK injects
// System.Drawing and System.Windows.Forms as implicit usings. That makes plain names like
// Brush, Color, Point, Size and Button ambiguous with their System.Windows counterparts,
// which is why every one of them is pinned here.
//
// A global using *alias* may be declared only once per compilation. Previously these were
// spread over nine files (V12*/V13*/GlobalAliases*), which between them declared Brush four
// times and Button three times, so the assembly could not compile at all (CS1537). The set
// below is the exact union of those files, so nothing lost an alias it relied on; the
// superseded files are listed under <Compile Remove> in DeskCanvas.App.csproj.
//
// Add new aliases here, and nowhere else.

global using System.IO;
global using System.Net.Http;

global using Application = System.Windows.Application;
global using AutomationProperties = System.Windows.Automation.AutomationProperties;
global using Border = System.Windows.Controls.Border;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using Color = System.Windows.Media.Color;
global using Cursors = System.Windows.Input.Cursors;
global using DataFormats = System.Windows.DataFormats;
global using DragDropEffects = System.Windows.DragDropEffects;
global using DragEventArgs = System.Windows.DragEventArgs;
global using Geometry = System.Windows.Media.Geometry;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Orientation = System.Windows.Controls.Orientation;
global using Point = System.Windows.Point;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using Size = System.Windows.Size;
global using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
