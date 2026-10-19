using System;
using DazPose.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public sealed class InterestRowView : MonoBehaviour
    {
        public TMP_Text nameText, detailText;
        public Toggle[] frequencies, sentiments;
        public void Bind(KinkDefinition definition,KinkPreference value,Action<KinkFrequency> frequency,Action<KinkSentiment> sentiment)
        {
            var detail=GetComponent<CatalogDetailView>()??gameObject.AddComponent<CatalogDetailView>();
            detail.Details=definition.displayName+" ("+definition.id+")\n"+definition.description;
            nameText.text=definition.displayName;detailText.text=string.Join(" · ",definition.themeTags??Array.Empty<string>());
            var f=new[]{KinkFrequency.No,KinkFrequency.Normal,KinkFrequency.Frequent,KinkFrequency.VeryFrequent};
            var s=new[]{KinkSentiment.Loved,KinkSentiment.Neutral,KinkSentiment.Punishment};
            if(frequencies.Length!=4 || sentiments.Length!=3)throw new InvalidOperationException("Interest row needs four explicit frequency and three sentiment controls.");
            for(int i=0;i<f.Length;i++)
            {
                int n=i;frequencies[i].onValueChanged.RemoveAllListeners();
                frequencies[i].SetIsOnWithoutNotify(value.frequency==f[i]);
                frequencies[i].onValueChanged.AddListener(on=>{if(on)frequency(f[n]);});
            }
            for(int i=0;i<s.Length;i++)
            {
                int n=i;sentiments[i].onValueChanged.RemoveAllListeners();
                sentiments[i].SetIsOnWithoutNotify(value.sentiment==s[i]);
                sentiments[i].onValueChanged.AddListener(on=>{if(on)sentiment(s[n]);});
            }
        }
    }
}
