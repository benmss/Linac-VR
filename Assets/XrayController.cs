using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XrayController : MonoBehaviour {

  public GameObject xrayEmitter;
  ParticleSystem ps1;

	// Use this for initialization
	void Start () {
		if (xrayEmitter) {
      ps1 = xrayEmitter.GetComponent<ParticleSystem>();
    }
	}
	
	// Update is called once per frame
	void Update () {
		bool s = Input.GetKeyUp("space");
    if (s && ps1) {
      if (ps1.isPlaying) {
        ps1.Stop();
      } else {
        ps1.Play();
      }
    }
	}
}
