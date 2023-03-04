namespace canker;
using canker.Model;
using System.Text;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

public partial class Results : ContentPage
{
    public Results(List<MemberInfo> results)
	{
		InitializeComponent();

		theGrid.ItemsSource = results;
	}

	private async void Export(object sender, EventArgs e)
	{
        var data = theGrid.ItemsSource as List<MemberInfo>;
        var csv = data.Select(x => string.Join(",", x));
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);

        IEnumerable<PropertyDescriptor> props = TypeDescriptor.GetProperties(typeof(MemberInfo)).OfType<PropertyDescriptor>();
        var header = string.Join(",", props.ToList().Select(x => x.Name));
        var valueLines = data.Select(row => string.Join(",", header.Split(',').Select(a => row.GetType().GetProperty(a).GetValue(row, null))));
      
        foreach (var v in valueLines)
        {
            await streamWriter.WriteLineAsync(v);
        }

        await streamWriter.FlushAsync();

         var fileSaverResult = await FileSaver.Default.SaveAsync("data.csv", stream, CancellationToken.None);
        if (fileSaverResult.IsSuccessful)
        {
            await Toast.Make($"The file was saved successfully to location: {fileSaverResult.FilePath}").Show();
        }
        else
        {
            await Toast.Make($"The file was not saved successfully with error: {fileSaverResult.Exception.Message}").Show();
        }
    }

}