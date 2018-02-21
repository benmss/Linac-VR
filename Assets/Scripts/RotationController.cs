using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationController : MonoBehaviour {


  public GameObject bedTop;
  public GameObject bedBase;
  public GameObject bedAnchor;
  public GameObject gantry;
  public GameObject bedStand;
  public GameObject bedTopUnderside;
  public GameObject bedBaseUnderside;
  public GameObject isoCenter;

  //Bed
  float bxRot;
  float bxRotMax = 60;
  float byRot;
  float byRotMax = 95;
  float bzRot;
  float bzRotMax = 60;

  float bxPos;
  float bxPosMax = 60;
  float bxPosMin = -80;
  float byPos;
  float byPosMin = -40;
  float byPosMax = 40;
  float bzPos;
  float bzPosMax = 25;

  //Gantry
  float gxRot;
  float gxRotMax = 180;

  Vector3 bedStandScale;

  Quaternion bedBaseQ;
  Quaternion bedTopQ;
  Quaternion bedAnchorQ;
  Quaternion gantryQ;


	void Start () {
    gantryQ = gantry.transform.rotation;
    bedStandScale = bedStand.transform.localScale;
	}

  public void RotateBed(int direction, float amount) {
    if (direction == 0) {
      //X - Rotate Y
      if (bxRot + amount > bxRotMax || bxRot + amount < -bxRotMax) { return; }
      // bedTopUnderside.transform.Rotate(amount*0.1f,0,0);
      bedBaseUnderside.transform.RotateAround(bedTop.transform.position,bedBaseUnderside.transform.right,amount*.1f);
      bxRot += amount;
    } else if (direction == 1) {
      //Y
      if (byRot + amount > byRotMax || byRot + amount < -byRotMax) { return; }
      // bedAnchor.transform.Rotate(0,amount,0);
      bedAnchor.transform.RotateAround(isoCenter.transform.position, bedAnchor.transform.forward, amount);
      byRot += amount;
    } else if (direction == 2) {
      //Z
      if (bzRot + amount > bzRotMax || bzRot + amount < -bzRotMax) { return; }
      // bedBaseUnderside.transform.Rotate(0,amount*.1f,0);
      bedBaseUnderside.transform.RotateAround(bedTop.transform.position,bedBaseUnderside.transform.up,amount*.1f);
      bzRot += amount;
    }
  }

  public void ResetBedRotation(int direction) {
    if (direction == 0) {
      //X
      bedBaseUnderside.transform.RotateAround(bedTop.transform.position,bedBaseUnderside.transform.right,-bxRot*.1f);
      bxRot = 0;
    } else if (direction == 1) {
      //Y
      bedAnchor.transform.RotateAround(isoCenter.transform.position, bedAnchor.transform.forward, -byRot);
      byRot = 0;
    } else if (direction == 2) {
      //Z
      bedBaseUnderside.transform.RotateAround(bedTop.transform.position,bedBaseUnderside.transform.up,-bzRot*.1f);
      bzRot = 0;
    }
  }

  public void MoveBed(int direction, float amount) {
    float amt = amount * 0.01f;
    if (direction == 0) {
      //Z - Due to rotation in scene
      if (bzPos + amount > bzPosMax || bzPos + amount < -bzPosMax) { return; }
      bedTop.transform.position += bedTop.transform.up * amt;
      bzPos += amount;
    } else if (direction == 1) {
      //Y
      if (byPos + amount > byPosMax || byPos + amount < byPosMin) { return; }
      bedBaseUnderside.transform.position = bedBaseUnderside.transform.position + new Vector3(0,amt,0);
      bedStand.transform.localScale += new Vector3(0,0,amt*2);
      byPos += amount;
    } else if (direction == 2) {
      //X
      if (bxPos + amount > bxPosMax || bxPos + amount < bxPosMin) { return; }
      bedTop.transform.position = bedTop.transform.position + bedTop.transform.right * amt;
      bxPos += amount;
    }
  }

  public void ResetBedMovement(int direction) {
    if (direction == 0) {
      //Z - Due to rotation in scene
      bedTop.transform.position -= bedTop.transform.up * bzPos *.01f;
      bzPos = 0;
    } else if (direction == 1) {
      //Y
      bedBaseUnderside.transform.position = bedBaseUnderside.transform.position + new Vector3(0,-byPos*.01f,0);
      bedStand.transform.localScale = bedStandScale;
      byPos = 0;
    } else if (direction == 2) {
      //X
      bedTop.transform.position = bedTop.transform.position - bedTop.transform.right * bxPos * .01f;
      bxPos = 0;
    }

  }

  public void ResetGantry() {
    gantry.transform.RotateAround(isoCenter.transform.position, gantry.transform.up, -gxRot);
    gxRot = 0;
  }

  public void RotateGantry(float amount) {

    if (gxRot + amount > gxRotMax || gxRot + amount < -gxRotMax) { return; }
    gantry.transform.RotateAround(isoCenter.transform.position, gantry.transform.up, amount);
    // gantry.transform.Rotate(amount,0,0);
    gxRot += amount;
  }



	// Update is called once per frame
	/* void Update () {
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
	} */
}
