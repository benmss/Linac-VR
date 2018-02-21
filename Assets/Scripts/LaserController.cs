﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LaserController : MonoBehaviour {


  public Transform isoCenter;

  HashSet<Material> mats = new HashSet<Material>();
  int testCounter = 0;
  int testMax = 100;
  bool on = false;
  Vector3 bed;
  Vector3 linac;
  float radius = 0.005f;
  List<LaserTarget> laserTargets = new List<LaserTarget>();
  HashSet<string> mn = new HashSet<string>();


	// Use this for initialization
	void Start () {
    LaserTarget[] lsa = Transform.FindObjectsOfType(typeof (LaserTarget)) as LaserTarget[];
    foreach (LaserTarget ls in lsa) {
      laserTargets.Add(ls);
      mats.Add(ls.gameObject.GetComponent<MeshRenderer>().sharedMaterial);
      // mn.Add(ls.gameObject.GetComponent<MeshRenderer>().material.name);
    }
    
    // foreach (string s in mn) {
      // print(s);
    // }

		// foreach (GameObject g in laserReceivers) {
      // if (g) {
        // mats.Add(g.GetComponent<Renderer>().material);
      // }
    // }

    // bed = bedAnchor.transform.position;
    // linac = linacAnchor.transform.position;
    AlignLasers();
	}
  
  void OnDestroy() {
    ChangeLasers(0);
  }

  public void Toggle() {
    on = !on;
    foreach (Material m in mats) {
      m.SetInt("_ON",(on == true ? 1 : 0));
    }
  }

  public void ChangeLasers(float v) {
    foreach (Material m in mats) {
      m.SetInt("_ON",((int)Math.Round(v)));
    }
  }

  void AlignLasers() {
    foreach (Material m in mats) {
      m.SetFloat("_xMin",isoCenter.position.x-radius);
      m.SetFloat("_xMax",isoCenter.position.x+radius);
      m.SetFloat("_yMin",isoCenter.position.y-radius);
      m.SetFloat("_yMax",isoCenter.position.y+radius);
      m.SetFloat("_zMin",isoCenter.position.z-radius);
      m.SetFloat("_zMax",isoCenter.position.z+radius);
    }
  }





	// Update is called once per frame
	void Update () {
		bool l = Input.GetKeyUp("l");
    bool t = Input.GetKeyUp("t");

    if (l) {
      on = !on;
      foreach (Material m in mats) {
        m.SetInt("_ON",(on == true ? 1 : 0));
      }
    }

    if (t && testCounter == -1) {
      testCounter = 0;
      foreach (Material m in mats) {
        m.SetInt("_OK",1);
      }
    }

    if (testCounter == testMax) {
      testCounter = -1;
      foreach (Material m in mats) {
        m.SetInt("_OK",0);
      }

    }

    if (testCounter >= 0) {
       testCounter++;
    }

	}
}
