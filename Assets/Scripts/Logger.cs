using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Globalization;

using UnityEngine;

public class Logger : MonoBehaviour {
  
  public static Logger logger;

	string errorLogName = "error.log";
  string outputLogName = "output.log";
  FileInfo errorLog;
  FileInfo outputLog;
  bool outputLogFailed = false;
  bool errorLogFailed = false;

  public bool logOutput = true;
  public bool logErrors = true;

  CultureInfo culture = new CultureInfo("en-GB");

  void Start() {
    logger = this;
    errorLog = new FileInfo(errorLogName);
    outputLog = new FileInfo(outputLogName);

    if (logOutput) {
      WriteOutput("Linac-VR log start.");
    }
    // print("Log Created: " + outputLog.FullName);
    
  }

  void OnDestroy() {
    if (logOutput) {
      WriteOutput("Linac-VR log end.\n");      
    }
  }
  
  public static void Error(Exception e) {
    logger.WriteError(e);
  }

  public void WriteError(Exception e) {
    if (!logErrors) { return; }
    if (errorLogFailed) { return; }

    if (errorLog == null ) {
      try {
        if (!errorLog.Exists) { errorLog.Create(); }
      } catch (Exception e1) {
        //Failed to create errorLog
        errorLog = null;
        errorLogFailed = true;
        return;
      }
    }

    try {
      using (StreamWriter sw = new StreamWriter(errorLog.Open(FileMode.Append, FileAccess.Write))) {
        sw.WriteLine("=====================================================================================");
        sw.WriteLine(DateTime.Now.ToString(culture) + ": Linac-VR has encountered an error, details follow:");
        sw.WriteLine(e);
        sw.WriteLine("=====================================================================================");
        sw.WriteLine();
      }
    } catch (Exception e2) {}
  }

  public static void Log(string msg) {
    logger.WriteOutput(msg);
  }
  
  public void WriteOutput(string msg) {
    if (!logOutput) { return; }
    if (outputLogFailed) { return; }

    if (outputLog == null ) {
      try {
        if (!outputLog.Exists) { outputLog.Create(); }
      } catch (Exception e) {
        //Failed to create outputLog
        outputLog = null;
        outputLogFailed = true;
        return;
      }
    }

    try {
      using (StreamWriter sw = new StreamWriter(outputLog.Open(FileMode.Append, FileAccess.Write))) {
        sw.WriteLine(DateTime.Now.ToString(culture) + ": " + msg);
        
      }
    } catch (Exception e) {}
  }
}
