using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIFollowHand : MonoBehaviour {

  public GameObject handHintModel;
  
  Vector3 offset = new Vector3(-0.017f,0.111f,0.187f);
  Vector3 rot = new Vector3(22.97f,0f,0f);

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		if (handHintModel) {
      transform.position = handHintModel.transform.position;
      transform.rotation = handHintModel.transform.rotation;      
    }
	}
}
