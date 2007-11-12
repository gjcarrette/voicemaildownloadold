/*-*-mode:java;c-basic-offset:2;indent-tabs-mode:nil-*-*

VoiceMailDownload, a utility to download voice mail from the GalaxyVoice.Com service.

    Copyright (C) 2007 George j. Carrette

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Resources;
using System.Reflection;

namespace VoiceMailDownload
{
  public class VoiceMailDownload
  {
    public System.IO.TextWriter stdout = System.Console.Out;
    public System.IO.TextWriter logfile = null;
    public string log_filename = null;
    public string test_filename = null;
    public string show_flag = null;
    public ResourceManager rm = null;
    public string RECLSTART = "<!-- ### Beginning Of Records ### -->";
    public string RECLEND = "<!-- ### End Of Records ### -->";
    
    public string download_path = null;
    
    public string user = "(null)";
    public string pass = "(null)";
    public string voicemail_url = null;
  
    public void Parse(string[] args)
    {
      for(int j=0;(j+1)<args.Length;j+=2)
        {
          string argname = args[j];
          string argvalue = args[j+1];
          switch(argname)
            {
            case  "-debug":
              break;
            case  "-pause":
              break;
            case "-test":
              test_filename = argvalue;
              break;
            case "-log":
              log_filename = argvalue.Replace("TIMESTAMP",DateTime.Now.ToString("s").Replace(":","-"));
              break;
            case "-user":
              user = argvalue;
              break;
            case "-pass":
              pass = argvalue;
              break;
            case "-url":
              voicemail_url = argvalue;
              break;
            case "-path":
              download_path = argvalue;
              break;
            case "-show":
              show_flag = argvalue;
              break;
            default:
              throw new Exception("Unknown argument " +
                                  argname + " " +
                                  getstring("helpstr"));
            }
        }
    }
    
    string getstring(string name)
    {
      if (rm == null)
        {
          rm = new ResourceManager(this.GetType().FullName,
                                   Assembly.GetExecutingAssembly());
        }
      string val = rm.GetString(name);
      return(val);
    }
    
    void showsource(string fname)
    {
      string s = getstring(fname);
      string k = "*" + "**********";
      msg(k + " start of " + fname + " " + k);
      msg(s);
      msg(k + " end of " + fname + " " + k);
    }
    
    public void Run()
    {
      if (log_filename != null)
        {
          msg("Opening log file " + log_filename);
          logfile = new StreamWriter(log_filename);
        }

      if (download_path == null)
        download_path = getstring("download_path");
      if (voicemail_url == null)
        voicemail_url = getstring("voicemail_url");
      
      msg(getstring("copyrightmsg"));
      
      if (show_flag != null)
        {
          string copyleft = getstring("gpl-3.0.txt");
          if (show_flag == "w")
            msg(substr(copyleft,
                       "Disclaimer of Warranty.",
                       "END OF TERMS AND CONDITIONS",
                       true,
                       true));
          else if (show_flag == "s")
            {
              string sourcefilenames = getstring("sourcefilenames");
              if (sourcefilenames != null)
                {
                  foreach(string s in sourcefilenames.Split('\n'))
                    {
                      string name = s.Trim();
                      if (name != "")
                        showsource(name);
                    }
                }
            }
          else if (show_flag == "h")
            {
              msg(getstring("helpstr"));
            }
          else
            msg(copyleft);
          return;
        }
      msg("Starting at " + DateTime.Now);
      List<voicemsg> l = get_voicemsg_list();
      msg("There are " + l.Count + " voice messages on the server");
      foreach(voicemsg m in l)
        {
          msg("boxname = [" + m.boxname + "]" +
              ", filename = [" + m.filename + "]" +
              ",title = [" + m.title + "]" +
              ",timestamp = [" + m.timestamp + "]" +
              ",duration = [" + m.duration + "]" +
              ",size = [" + m.size + "]");
          string local_pathname = download_path + m.LocalFileName();
          msg("Local Pathname = " + local_pathname);
          if (File.Exists(local_pathname))
            msg("already downloaded");
          else
            download_voicemail(m,local_pathname);
        }
    }
    
    void download_voicemail(voicemsg m,string local_pathname)
    {
      Stopwatch sw = new Stopwatch();
      sw.Start();
      string url = "voiceplayer.cgi?playmessage=y" +
        "&box=" + m.boxname +
        "&message=" + m.filename;
      HttpWebRequest req = getrequest(url);
      HttpWebResponse res  = (HttpWebResponse) req.GetResponse();
      int nbytes = 0;
      int nreads = 0;
      int nbytesprogress = 0;
      if (res.StatusCode == HttpStatusCode.OK)
        {
          BinaryReader reader = new BinaryReader(res.GetResponseStream());
          using(Stream output_file_stream = new FileStream(local_pathname + ".tmp",
                                                           FileMode.Create))
            {
              const int chunkSize = 32000;
              int n;
              byte[] buf = new byte[chunkSize];
              while ((n = reader.Read(buf,0,chunkSize)) > 0)
                {
                  nbytes += n;
                  nreads += 1;
                  output_file_stream.Write(buf,0,n);
                  if (nbytes > (chunkSize + nbytesprogress))
                    {
                      if (stdout != null) stdout.Write(".");
                      nbytesprogress = nbytes;
                    }
                }
            }
          File.Move(local_pathname + ".tmp",local_pathname);
        }
      sw.Stop();
      msg(nreads + " reads to get " + nbytes + " in " + sw.Elapsed);
    }
    
    string substr(string str,string key1,string key2,bool inner,bool ex)
    {
      int j = str.IndexOf(key1);
      if (j < 0)
        {
          if (ex)
            throw new Exception("could not find " + key1);
          return(null);
        }
      int k = str.IndexOf(key2,j+key1.Length);
      if (k < 0)
        {
          if (ex) throw new Exception("could not find " + key2);
          return(null);
        }
      int start = j;
      int len = k+key2.Length-j;
      if (inner)
        {
          start+=key1.Length;
          len -= (key1.Length+key2.Length);
        }
      return(str.Substring(start,len));
    }
    
    List<voicemsg> get_voicemsg_list()
    {
      List<voicemsg> result = new List<voicemsg>();
      string l1 = get_voicemsg_list_content();
      /* The content is not good html, cannot be parsed using any common .NET 
         libraries for dealing with markup languages */
      string l2 = substr(l1,RECLSTART,RECLEND,true,true);
      string[] l3 = l2.Split('\n');
      foreach(string l in l3)
        {
          voicemsg t = voicemsg_tryparse(l);
          if (t != null)
            result.Add(t);
        }
      return(result);
    }
    
    voicemsg voicemsg_tryparse(string l)
    {
      string boxname = substr(l,"<tr><td>","</td>",true,false);
      if (boxname == null) return(null);
      string filename = substr(l,
                               "box=" + boxname + "&message=",
                               "\"",
                               true,
                               false);
      if (filename == null) return(null);
      string title = substr(l,"<td align=center nowrap>","</td>",
                            true,false);
      if (title == null) return(null);
      string timestamp = substr(l,"<td nowrap>","</td>",true,false);
      if (timestamp == null) return(null);
      string duration = substr(l,"<td align=center>","</td>",
                               true,false);
      if (duration == null) return(null);
      string sizek = substr(l,duration+"</td><td>","</td>",true,false);
      if (sizek == null) return(null);
      voicemsg result = new voicemsg();
      result.boxname = boxname;
      result.filename = filename;
      result.title = title;
      result.timestamp = timestamp;
      result.duration = duration;
      result.size = sizek.Trim();
      return(result);
    }
    
    string get_voicemsg_list_content()
    {
      if (test_filename != null)
        {
          msg("opening " + test_filename);
          using(StreamReader s = new StreamReader(test_filename))
            {
              return(s.ReadToEnd());
            }
        }
      else
        {
          msg("Contacting server " + voicemail_url);
          HttpWebRequest req = getrequest("voicemail.cgi");
          HttpWebResponse res  = (HttpWebResponse) req.GetResponse();
          Stream res_stream = res.GetResponseStream();
          string res_text = (new StreamReader(res_stream)).ReadToEnd();
          res_stream.Close();
          return(res_text);
        }
    }
    
    HttpWebRequest getrequest(string url)
    {
      Uri srv_uri = new Uri(voicemail_url);
      Uri uri = new Uri(voicemail_url + url);
      HttpWebRequest req = (HttpWebRequest) WebRequest.Create(uri);
      CookieContainer init_cookies = new CookieContainer();
      req.CookieContainer = init_cookies;
      init_cookies.Add(srv_uri,new Cookie("user",user));
      init_cookies.Add(srv_uri,new Cookie("pass",pass));
      req.KeepAlive = false;
      req.AllowAutoRedirect = false;
      return(req);
    }
    
    public void Close(Exception e)
    {
      if (e != null)
        msg(e.GetType().FullName + ": " + e.Message);
      msg("Done at " + DateTime.Now);
      if (logfile != null)
        {
          logfile.Close();
          logfile = null;
        }
    }
    
    public void msg(string s)
    {
      if (stdout != null) stdout.WriteLine(s);
      if (logfile != null) logfile.WriteLine(s);
    }
    
    static int Main(string[] args)
    {
      bool debugflag = false;
      bool pauseflag = false;
      int retval = 0;
      for(int j=0;(j+1)<args.Length;j+=2)
        {
          switch (args[j])
            {
            case "-debug":
              if (args[j+1] == "true") debugflag = true;
              break;
            case "-pause":
              if (args[j+1] == "true") pauseflag = true;
              break;
            default:
              break;
            }
        }
      VoiceMailDownload p = null;
      if (!debugflag)
        {
          try
            {
              p = new VoiceMailDownload();
              p.Parse(args);
              p.Run();
              p.Close(null);
              retval = 0;
            }
          catch (Exception e1)
            {
              try
                {
                  if (p != null) p.Close(e1);
                }
              catch (Exception e2)
                {
                  Console.WriteLine("error handling exception, " +
                                    e2.GetType().FullName +
                                    ": " + e2.Message);
                  Console.WriteLine("Original error, " +
                                    e1.GetType().FullName + ": " +
                                    e1.Message);
                }
              retval = 1;
            }
        }
      else
        {
          p = new VoiceMailDownload();
          p.Parse(args);
          p.Run();
          p.Close(null);
          retval = 0;
        }
      if (pauseflag)
        {
          Console.WriteLine("(PAUSE)");
          Console.ReadLine();
        }
      return(retval);
    }

    public class voicemsg
    {
      public string boxname = null;
      public string filename = null;
      public string title = null;
      public string timestamp = null;
      public string duration = null;
      public string size = null;
      
      public string LocalFileName()
      {
        string tmp = title + "-" + timestamp + ".wav";
        tmp = tmp.Replace("\"","");
        tmp = tmp.Replace("<","");
        tmp = tmp.Replace(">","");
        tmp = tmp.Replace("  "," ");
        tmp = tmp.Replace(" ","-");
        tmp = tmp.Replace(":","-");
        tmp = tmp.Replace(",","-");
        return(tmp);
      }
    }
  }
}
