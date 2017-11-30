using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour {

  public bool showLaser = true;
  ParticleSystem ps1;
  ParticleSystem ps2;
  ParticleSystem s1;
  ParticleSystem s2;
  bool running = false;
  bool valid = false;
  Color green = new Color(0.05f,0.8f,0.09f,0.3f);
  Color red = new Color(0.8f,0.05f,0.09f,0.3f);
  
  

	// Use this for initialization
	void Start () {
		ps1 = transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
    s1 = ps1.gameObject.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
    ps2 = transform.GetChild(1).gameObject.GetComponent<ParticleSystem>();
    s2 = ps2.gameObject.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
    
    if (showLaser) {
      ShowLasers();
    }
	}
	
	// Update is called once per frame
	void Update () {
		if (showLaser && !running) {
     ShowLasers();
    }
  }
  
  void ShowLasers() {
    ps1.Play();
    ps2.Play();
    running = true;
  }
  
  void changeColour(bool valid) {
    if (this.valid != valid) {
      ParticleSystem.MainModule m1 = s1.main;
      ParticleSystem.MainModule m2 = s2.main;
      if (valid) {
        m1.startColor = green;
        m2.startColor = green;
      } else {
        m1.startColor = red;
        m2.startColor = red;
      }
      this.valid = valid;
    }
  }
  
  
}
