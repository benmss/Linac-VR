﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinacRotation : MonoBehaviour {

  public Vector3 rotation = new Vector3(1,0,0);
  public bool rotate = false;
  float angle = 0;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
    
    
	}
  
  public void RotateLeft() {
    if (angle > -180) {
      this.transform.Rotate(-rotation);
      angle--;
    }
  }
  
  public void RotateRight() {
    if (angle < 180) {
        this.transform.Rotate(rotation); 
        angle++;
      }
  }
  
  public void RotateToCenter() {
    if (angle < 0) {
      this.transform.Rotate(rotation); 
      angle++;
    } else if (angle > 0) {
      this.transform.Rotate(-rotation); 
      angle--;
    }
  }
  
  
  
}
