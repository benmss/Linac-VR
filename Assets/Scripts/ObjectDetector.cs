using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectDetector : MonoBehaviour {

  public GameObject listener;
  public string addMethod;
  public string removeMethod;
  
  
  void OnTriggerEnter(Collider other) {
    listener.BroadcastMessage(addMethod,other.gameObject);
  }
  void OnTriggerExit(Collider other) {
    // listener.BroadcastMessage(removeMethod,other.gameObject);
  }
}
