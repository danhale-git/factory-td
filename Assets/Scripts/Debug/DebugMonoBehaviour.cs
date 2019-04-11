using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugMonoBehaviour : MonoBehaviour
{
    public Text debugText;    
    public Dictionary<string, string> debugTextEntries = new Dictionary<string, string>();
    public Dictionary<string, int> debugTextCountEntries = new Dictionary<string, int>();

    void Start()
    {
        
    }

    void Update()
    {
        string newText = "";

        Dictionary<string, string> debugTextEntriesCopy = new Dictionary<string, string>(debugTextEntries);
        Dictionary<string, int> debugTextCountEntriesCopy = new Dictionary<string, int>(debugTextCountEntries);

        foreach(KeyValuePair<string, string> kvp in debugTextEntriesCopy)
        {
            newText += kvp.Key+": "+kvp.Value+"\n";
        }

        foreach(KeyValuePair<string, int> kvp in debugTextCountEntriesCopy)
        {
            newText += kvp.Key+": "+kvp.Value.ToString()+"\n";
        }

        debugText.text = newText;
    }
}
