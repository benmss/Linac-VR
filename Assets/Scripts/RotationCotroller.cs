using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationCotroller : MonoBehaviour {
  
  public LinacRotation GantryRotationScript;
  public BedRotation BedRotationScript;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		bool l = Input.GetKey("left");
    bool r = Input.GetKey("right");
    bool u = Input.GetKey("up");
    bool d = Input.GetKey("down");
    
    
    //Gantry rotation
    if (l && !r) {
      GantryRotationScript.RotateLeft();
    } else if (!l && r) {
      GantryRotationScript.RotateRight();
    } else if (l && r) {
      GantryRotationScript.RotateToCenter();
    }
    
    //Bed rotation
    if (u && !d) {
      BedRotationScript.RotateLeft();
    } else if (!u && d) {
      BedRotationScript.RotateRight();
    } else if (u && d) {
      BedRotationScript.RotateToCenter();
    }
	}
}
