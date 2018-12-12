using UnityEngine;
using UnityEngine.Networking;
using Terrain.Tiles;
using System.Collections;
using System.IO;
using ExtensionMethods;
using System;

public class viewer : MonoBehaviour {

    //Vector2 location = new Vector2(16918, 12911);
    //amsterdam test  52.3616256,4.9053696
    //Vector2 location = new Vector2(4.910889f, 52.36048f);
    Vector2 MeterPerGraad = new Vector2();
    //Vector2 location = new Vector2(5.8698243f, 51.8494942f);   //nijmegen zoomfactor 16

    public GameObject maaiveldgroep;
    GameObject tile;
    public void CreateTile()
    {
        //boundingbox Amsterdam
        Vector2 TopLeft = new Vector2(4.728958f, 52.435088f); // de werkelijke top-left corner van amsterdam
        //TopLeft = new Vector2(4.875974f, 52.399172f);//topleft van het centrum
        Vector2 BottomRight = new Vector2(5.021642f, 52.303762f);
        //te gebruiken zoomfactor
        float zoomfactor = 13f;

        //locatie van de Origin (Unity-Nul) in wgs84 coordinaten
        Vector2 OriginWGS84 = new Vector2(4.272455f, 52.44119f);
        //aantal meters per graad lon en lat ter plaatse van Amsterdam
        MeterPerGraad = new Vector2(68120.5f, 111265.7f);
        // tijdelijk aanpassen om vierkant te tonen
        //MeterPerGraad = new Vector2(68120.5f, 68120.5f);

        //tegelgrootte in graden lon en lat
        Vector2 TileSizeWGS84 = new Vector3();
        TileSizeWGS84.x = 180f / (UnityEngine.Mathf.Pow(2f, zoomfactor));
        TileSizeWGS84.y = 180f / (UnityEngine.Mathf.Pow(2f, zoomfactor));

        // aantal tilergels en tilekolommen binnen de Boundingbox
        int aantal_TileRegels;
        int aantal_Tilekolommen;
        aantal_TileRegels = (int)UnityEngine.Mathf.Floor((TopLeft.y - BottomRight.y) / TileSizeWGS84.y);
        aantal_Tilekolommen = (int)UnityEngine.Mathf.Floor((BottomRight.x - TopLeft.x) / TileSizeWGS84.x);

        // in te stellen schaalfactor voor de tegels zodat deze in (eens soort van) rd-coordinaten in Unity komen
        Vector2 tilescale = new Vector2();
        tilescale.x = MeterPerGraad.x/UnityEngine.Mathf.Pow(2f, zoomfactor);
        tilescale.y = MeterPerGraad.y / UnityEngine.Mathf.Pow(2f, zoomfactor);

        //gridlocatie van eerste Tile
        Vector2 TileGridLocation = GetTileLocation(TopLeft.x, TopLeft.y, zoomfactor);

        //aantal_Tilekolommen = 7; //tijdelijk: aantal weer te geven tegelkolommen
        //aantal_TileRegels = 7; // tijdelijk: aantal weer te geven regels
        for (int i = 0; i < aantal_Tilekolommen; i++)
        {
            for (int j = 0; j < aantal_TileRegels; j++)
            {

                Vector2 TileOriginWGS84 = new Vector2();
                TileOriginWGS84.x = ((TileGridLocation.x + i) * TileSizeWGS84.x) - 180;
                TileOriginWGS84.y = ((TileGridLocation.y-j) * TileSizeWGS84.y) - 90;
                Vector2 TileOriginLOCAL = new Vector2();
                TileOriginLOCAL.x = (TileOriginWGS84.x - OriginWGS84.x) * MeterPerGraad.x;
                TileOriginLOCAL.y = (TileOriginWGS84.y-OriginWGS84.y) * MeterPerGraad.y;
                StartCoroutine(requestQMTile((int)TileGridLocation.x + i, (int)TileGridLocation.y-j, (int)zoomfactor, TileOriginLOCAL, tilescale, TileOriginWGS84, TileSizeWGS84));
            }
        }

    }

    private IEnumerator requestQMTile(int x, int y, int z, Vector2 location, Vector2 tilescale,Vector2 TileOriginWGS84, Vector2 TileSizeWGS)
    {
        string url = $"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0";
        
        DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
        TerrainTile terrainTile;
        UnityWebRequest http = new UnityWebRequest(url);
  
        http.downloadHandler = handler;
        yield return http.SendWebRequest();

        if (!http.isNetworkError)
        {
            //get data
            MemoryStream stream = new MemoryStream(http.downloadHandler.data);

            //parse into tile data structure
            terrainTile = TerrainTileParser.Parse(stream);

            //create unity tile with tile data structure
            
            tile = new GameObject("tile x:" + x + " y:" + y + " z:" + z);
            tile.transform.parent = maaiveldgroep.transform;
            tile.AddComponent<MeshFilter>().mesh = terrainTile.GetMesh();
            tile.AddComponent<MeshCollider>();
            //scale
            tile.transform.localScale = new Vector3(-1*tilescale.x/2,-1, -1*tilescale.y);

            tile.transform.position = new Vector3((location.x), 0, (location.y));
            StartCoroutine(requestWMSTile(location.x, location.y,tile,TileOriginWGS84, TileSizeWGS));
        }
        else
        {
            Debug.Log("Error loading tile");
        }
    }

    private IEnumerator requestWMSTile(float lng, float lat, GameObject tile,Vector2 TileOriginWGS84, Vector2 TileSizeWGS)
    {


        // url string opbouwen voor het ophalen van de bgt-afbeelding
        string url = "https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox=";
        url += TileOriginWGS84.x;
        url += "%2C" + TileOriginWGS84.y;
        url += "%2C" + (TileOriginWGS84.x + TileSizeWGS.x);
        url += "%2C" + (TileOriginWGS84.y + TileSizeWGS.y);
        url += "&width=" + 1024;
        url += "&height=" + 1024;
        url+="&srs=EPSG%3A4326";
        url = url.Replace(",", ".");
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (!www.isNetworkError || !www.isHttpError)
        {
            Texture2D myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            myTexture.filterMode = FilterMode.Point;
            
            var tempMaterial = new Material(Shader.Find("Unlit/Texture"));
            tempMaterial.mainTexture = myTexture;
            tempMaterial.SetTextureScale("_MainTex", new Vector2(-0.00278f, -0.00555f));//waarden empirisch bepaalt, niet gecontroleerd of dit ook werkt bij andere zoomfactor
            tempMaterial.SetTextureOffset("_MainTex", new Vector2(-0.5f, 0.5f));
            
            tile.AddComponent<MeshRenderer>().sharedMaterial = tempMaterial;
        }
        else
        {
            Debug.Log("Error loading tile");
        }
    }

    private Vector2 GetTileLocation(double rdX, double rdY, float zoomfactor)
    {
        Vector2 TileLocation = new Vector2();
        int tilewidth = 256;
        double scaleDenominator = 0.703125 / Math.Pow(2,zoomfactor);
        Debug.Log("scaleDenominator bij zoomfactor "+ zoomfactor + " is "+scaleDenominator);
        
        double OriginX = (rdX+180)/ scaleDenominator;
        Debug.Log("originX is " + OriginX);

        double OriginY = (rdY+90) / scaleDenominator;
        Debug.Log("originY is " + OriginY);

        double a = OriginX / tilewidth;
        double b = OriginY / tilewidth;

        double c = Math.Floor(a);
        double d = Math.Floor(b);
        TileLocation.x = (float)c;
        TileLocation.y = (float)d;

        return TileLocation;
    }
}