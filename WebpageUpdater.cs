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

	private Dictionary<string, dynamic> data;

    private Action<string, HttpContext> updateAction;


	public WebpageUpdater(string filename, ref Dictionary<string, dynamic> data, Action<string, HttpContext> updateAction)
    {
        originalHtml = File.ReadAllText(filename);
        this.data = data;
        this.updateAction = updateAction;
    }

    public string GetUpdated() 
    {
        string html = originalHtml;
        for (int i = 0; i < data.Count; i++)
        {
            var currentPlaceholder = data.ElementAt(i);
            html = html.Replace(currentPlaceholder.Key, currentPlaceholder.Value.ToString(), StringComparison.Ordinal);
        }
        LastUpdated = html;
        return html;
    }

    public void Update(HttpContext context) 
    {
        string updated = GetUpdated();
        updateAction?.Invoke(updated, context);
    }
}
