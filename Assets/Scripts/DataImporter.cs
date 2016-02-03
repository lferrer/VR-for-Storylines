using UnityEngine;
using System.Collections;
using System.Linq;
using System.IO;

public class DataImporter : MonoBehaviour {

	// Use this for initialization
	void Start () {
        var lines = File.ReadAllLines("test.txt").Select(a => a.Split(';'));
        var csv = from line in lines
                  select (from piece in line
                          select piece);
    }
	
	// Update is called once per frame
	void Update () {
	
	}
}
