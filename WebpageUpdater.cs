using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pumpkin.PiCollectionServer;
internal class WebpageUpdater
{
    public string LastUpdated { get; private set; } = string.Empty;

	private string originalHtml;

	private Dictionary<string, Func<dynamic>> data;

    private Action<string, HttpContext> updateAction;


	public WebpageUpdater(string filename, ref Dictionary<string, Func<dynamic>> data, Action<string, HttpContext> updateAction)
    {
        originalHtml = File.ReadAllText(filename);
        this.data = data;
        this.updateAction = updateAction;
    }

	public WebpageUpdater(string filename, ref Dictionary<string, Func<dynamic>> data)
	{
		originalHtml = File.ReadAllText(filename);
		this.data = data;
	}

	public string GetUpdated() 
    {
        string html = originalHtml;
        for (int i = 0; i < data.Count; i++)
        {
            var currentPlaceholder = data.ElementAt(i);
            dynamic value = currentPlaceholder.Value?.Invoke();

			html = html.Replace(currentPlaceholder.Key, value.ToString() ?? "Whoops! Error", StringComparison.Ordinal);
        }
        LastUpdated = html;
        return html;
    }

    public void Update(HttpContext context) 
    {
        if (updateAction is null) return;
        string updated = GetUpdated();
        updateAction(updated, context);
    }
}
