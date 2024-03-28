using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CsvHelper;

public class FatClinicalDrugs
{
    [JsonProperty("items")]
    public List<ClinicalDrug> ClinicalDrugs { get; set; }

}

public class ClinicalDrug
{
    [JsonProperty("referencedComponentId")]
    public string SctId { get; set; }
}

public class SnowConcepts
{
    [JsonProperty("items")]
    public List<Concept> Concepts { get; set; }

    [JsonProperty("total")]
    public int Count { get; set; }
}

public class Concept
{
    [JsonProperty("conceptId")]
    public string SctId { get; set; }
}

public class MpfMapping
{
    public List<MpfMap> MpfMaps { get; set; }
}


public class MpfMap
{
    public string CdId { get; set; }
    public string MpfId { get; set; }
}

public class MpMapping
{
    public List<MpMap> MpMaps { get; set; }
}

public class MpMap
{
    public string CdId { get; set; }
    public string MpId { get; set; }
}


class Program
{
    public static string baseUrl= "https://snowstorm.terminologi.ehelse.no/snowstorm/snomed-ct/";
    public static string refsetBranch = "MAIN/SNOMEDCT-NO/REFSETS";
    public static string refsetId = "88791000202108";
    static async Task Main(string[] args)
    {
        List<MpfMap> mpfMaps = new();
        List<MpMap> mpMaps = new();
        List<ClinicalDrug> clinicalDrugs = await FatCdLookup();


        foreach (var clinicalDrug in clinicalDrugs)
        {
            Concept mpf = await FindMPF(clinicalDrug);

            mpfMaps.Add(new MpfMap
            {
                CdId = clinicalDrug.SctId,
                MpfId = mpf.SctId
            }
                );

            string entryMpf = $"CD|{clinicalDrug.SctId}|MPF|{mpf.SctId}";
            string csvFilePathMpf = "C:\\temp\\cd_mpf_map.csv";
            WriteCsv(entryMpf, csvFilePathMpf);

            Concept mp = await FindMP(clinicalDrug);

            mpMaps.Add(new MpMap
            {
                CdId = clinicalDrug.SctId,
                MpId = mp.SctId
            }
            );

            string entryMp = $"CD|{clinicalDrug.SctId}|MP|{mpf.SctId}";
            string csvFilePathMp = "C:\\temp\\cd_mp_map.csv";
            WriteCsv(entryMp, csvFilePathMp);

        }

    }

    static void WriteCsv(string entry, string csvFilePath)
    {
        using (StreamWriter writer = new StreamWriter(csvFilePath, true))
        {
            writer.WriteLine(entry);
        }
    }

    static async Task<List<ClinicalDrug>> FatCdLookup()
    {
        List<ClinicalDrug> clinicalDrugs = new();

        string fatCdUrl = $"{baseUrl}{refsetBranch}/members?referenceSet={refsetId}&limit=50";

        try
        {
            using HttpClient fatCdClient = new HttpClient();

            HttpResponseMessage fatCdResponse = await fatCdClient.GetAsync(fatCdUrl);

            if (fatCdResponse.IsSuccessStatusCode)
            {
                string fatCdJson = await fatCdResponse.Content.ReadAsStringAsync();

                var fatCdResult = JsonConvert.DeserializeObject<FatClinicalDrugs>(fatCdJson);

                if(fatCdResult.ClinicalDrugs != null)
                {
                    foreach (var clinicalDrug in fatCdResult.ClinicalDrugs)
                    {
                        clinicalDrugs.Add(new ClinicalDrug
                        {
                            SctId = clinicalDrug.SctId
                        }
                        );
                        Console.WriteLine($"Clinical drug to test: {clinicalDrug.SctId}");
        
                    }
                }
            }
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex.Message);
        }
        return clinicalDrugs;
    }

    static async Task<Concept> FindMPF(ClinicalDrug clinicalDrug)
    {
        Concept mpf = new();

        string mpfUrl = $"{baseUrl}{refsetBranch}/concepts?activeFilter=true&ecl=%3E{clinicalDrug.SctId}%3A1142139005%3D%2A%2C411116001%3D%2A%2C%5B0..0%5D732943007%3D%2A&limit=50";

        Console.WriteLine($"{mpfUrl}");

        try
        {
            using HttpClient mpfClient = new();

            HttpResponseMessage mpfResponse = await mpfClient.GetAsync(mpfUrl);

            if (mpfResponse.IsSuccessStatusCode)
            {
                string mpfJson = await mpfResponse.Content.ReadAsStringAsync();

                var mpfResult = JsonConvert.DeserializeObject<SnowConcepts>(mpfJson);

                if (mpfResult.Count == 1)
                {

                    Console.WriteLine("1 MPF only");

                    mpf = mpfResult.Concepts[0];

                    Console.WriteLine($"Simple MPF: {mpf.SctId}");
                }

                if (mpfResult.Count > 1)
                {
                    Console.WriteLine("More than one MPF");

                    string mpfUrlComplex = $"{baseUrl}{refsetBranch}/concepts?activeFilter=true&ecl=%3E{clinicalDrug.SctId}%3A1142139005%3D%2A%2C411116001%3D%2A%2C%5B0..0%5D732943007%3D%2A%2C127489000%3D%28%28%28{clinicalDrug.SctId}.762949000%29%20OR%20%28%28{clinicalDrug.SctId}.762949000%29.738774007%29%20OR%20%28%28%28{clinicalDrug.SctId}.762949000%29.738774007%29.738774007%29%29%3A%5B0..0%5D738774007%3D%2A%29&limit=50";
                    
                    using HttpClient mpfClientComplex = new();

                    HttpResponseMessage mpfResponseComplex = await mpfClientComplex.GetAsync(mpfUrlComplex);
                    
                    if (mpfResponseComplex.IsSuccessStatusCode)
                    {
                        string mpfJsonComplex = await mpfResponseComplex.Content.ReadAsStringAsync();

                        var mpfResultComplex = JsonConvert.DeserializeObject<SnowConcepts>(mpfJsonComplex);

                        if (mpfResultComplex.Count == 1)
                        {
                            mpf = mpfResultComplex.Concepts[0];

                            Console.WriteLine($"Complex MPF: {mpf.SctId}");
                        }

                        else
                        {
                            Console.WriteLine($"Complex search for MPF gave {mpfResultComplex.Count} for CD {clinicalDrug.SctId}");

                            string mpfErrorEntry = $"Complex search for MPF gave {mpfResultComplex.Count} for CD {clinicalDrug.SctId}";
                            string mpfErrorCsvFilePath = "C:\\temp\\ErrorsComplexMpfSearch.csv";
                            WriteCsv(mpfErrorEntry, mpfErrorCsvFilePath);
                        };
                    }

                }

                else
                {

                }
            }

            
        }

        catch(Exception ex)
        {
            Console.WriteLine(ex);
        }

        return mpf;
    }

    static async Task<Concept> FindMP(ClinicalDrug clinicalDrug)
    {
        Concept mp = new();

        string mpUrl = $"{baseUrl}{refsetBranch}/concepts?activeFilter=true&ecl=%3E{clinicalDrug.SctId}%3A1142139005%3D%2A%2C%5B0..0%5D411116001%3D%2A%2C%5B0..0%5D732943007%3D%2A&limit=50";

        Console.WriteLine($"{mpUrl}");

        try
        {
            using HttpClient mpClient = new();

            HttpResponseMessage mpResponse = await mpClient.GetAsync(mpUrl);

            if (mpResponse.IsSuccessStatusCode)
            {
                string mpJson = await mpResponse.Content.ReadAsStringAsync();

                var mpResult = JsonConvert.DeserializeObject<SnowConcepts>(mpJson);

                if (mpResult.Count == 1)
                {

                    Console.WriteLine("1 MP only");

                    mp = mpResult.Concepts[0];

                    Console.WriteLine($"Simple MP: {mp.SctId}");
                }

                if (mpResult.Count > 1)
                {
                    Console.WriteLine("More than one MP");

                    string mpUrlComplex = $"{baseUrl}{refsetBranch}/concepts?activeFilter=true&ecl=%3E{clinicalDrug.SctId}%3A1142139005%3D%2A%2C%5B0..0%5D411116001%3D%2A%2C%5B0..0%5D732943007%3D%2A%2C127489000%3D%28%28%28{clinicalDrug.SctId}.762949000%29%20OR%20%28%28{clinicalDrug.SctId}.762949000%29.738774007%29%20OR%20%28%28%28{clinicalDrug.SctId}.762949000%29.738774007%29.738774007%29%29%3A%5B0..0%5D738774007%3D%2A%29&limit=50";

                    using HttpClient mpClientComplex = new();

                    HttpResponseMessage mpResponseComplex = await mpClientComplex.GetAsync(mpUrlComplex);

                    if (mpResponseComplex.IsSuccessStatusCode)
                    {
                        string mpJsonComplex = await mpResponseComplex.Content.ReadAsStringAsync();

                        var mpResultComplex = JsonConvert.DeserializeObject<SnowConcepts>(mpJsonComplex);

                        if (mpResultComplex.Count == 1)
                        {
                            mp = mpResultComplex.Concepts[0];

                            Console.WriteLine($"Complex MP: {mp.SctId}");
                        }

                        else
                        {
                            Console.WriteLine($"Complex search for MP gave {mpResultComplex.Count} for CD {clinicalDrug.SctId}");

                            string mpErrorEntry = $"Complex search for MP gave {mpResultComplex.Count} for CD {clinicalDrug.SctId}";
                            string mpErrorCsvFilePath = "C:\\temp\\ErrorsComplexMpSearch.csv";
                            WriteCsv(mpErrorEntry, mpErrorCsvFilePath);
                        };
                    }

                }

                else
                {

                }
            }


        }

        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return mp;
    }

}


