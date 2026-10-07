# 🗂️ CHAPTER 1: Project Overview & Architecture
This project shows how to monitor a Linux server, catch security threats in real time, and send that data to a central security platform (**Splunk Enterprise**)  so we can see how attack is working and looking

Instead of just using basic, ready made tools, I wanted to build something myself. I wrote a custom C# automation script ("Cyber Security Copilot") 
that runs in the background on a **Kali Linux** machine. 
The script constantly listens to the system logs, automatically pulls out failed login attempts using Regular Expressions (Regex), 
and converts that messy text into clean JSON data. Then, it ships the data safely over HTTPS to Splunk, where we can track the attacks on a live dashboard.
<img width="900" height="600" alt="image" src="https://github.com/user-attachments/assets/1afda6bb-5508-4e51-9f8b-78a62a5eafa4" />



# 🛠️ Environment & Tools
*   **SIEM Platform:** Splunk Enterprise (Running inside a Docker container)
*   **Log Receiver:** Splunk HTTP Event Collector (HEC) via Port `8088`
*   **Automation Agent:** Custom C# Linux Monitoring Script ("Cyber Security Copilot")
*   **Log Sources:** Linux Authentication Telemetry (`journalctl` / `sshd` framework)
*   **Mapped MITRE ATT&CK Techniques:**
    *   **Credential Access / Brute Force:** https://attack.mitre.org/techniques/T1110/001/
    *   **Collection / Input Capture:** Active Log Monitoring

---

## 🎯 Lab Scenario & Objective
As a Cybersecurity Student, the purpose of this project is to practice real world incident response, pipeline engineering, and threat hunting methodologies. To make this laboratory effective, I am trying to simulate a real world Security Operations Center (SOC) scenario.

*   **The Scenario:** A security alert has flagged anomalous network and authentication activity on a critical Linux endpoint (`kali`). The server is experiencing a high volume of failed login attempts over short intervals, indicating a potential automated attack on the SSH service (Port 22). 
*   **My Role:** In this simulated exercise, I am acting as both the Adversary (Red Team) and the Security Engineer / Incident Responder (Blue Team). I am responsible for launching the simulated SSH brute-force attack, deploying my custom C# monitoring agent to capture the events, fixing any pipeline errors between the (host/me) and the SIEM, and analyzing the final logs in Splunk.
*   **The Goal:** To simulate a realistic threat vector, trace the attacker's footprints, ensure a stable and secure data flow from the Linux system to our Dockerized Splunk instance, and verify that the threat telemetry is correctly indexed and visible to the defense team.🏛️ CHAPTER 2: Ingestion & Data Pipeline Mechanics

## 🏛️ CHAPTER 2: Ingestion & Data Pipeline Mechanics

### What I Did

* **Live Ingestion:** I configured the C# agent to spawn an asynchronous child process that streams live operating system logs directly from the kernel source using the journalctl -u ssh -f --no-pager command.
* **Regex Parsing:** I implemented Regular Expressions (Regex) within the application logic to scan the incoming log stream in real time and automatically extract critical metadata fields (source IP addresses and targeted usernames) from failed authentication attempts.
* **JSON Transformation & Secure Export:** I programmed the agent to structure these extracted parameters into structured JSON objects, which are then transmitted over HTTPS directly to the containerized Splunk SIEM via the HTTP Event Collector (HEC) interface on port 8088.

### Why I Did It

* **Performance Optimization:** I chose to stream the logs live (-f) instead of continuously polling, opening, and closing a static text file. This approach eliminates unnecessary disk read/write overhead (I/O performance drops) on the host machine and honestly, it was just more fun this way.
* **Efficient SIEM Indexing:** Shifting unstructured raw text to a SIEM requires writing heavy and compute expensive search time extractions (rex) for every single query. By converting the data into JSON at the agent level before transmission, Splunk indexes fields like source_ip and target_user automatically, resulting in high performance dashboards and just simpler queries.
* **Data Integrity & Line Security:** I separated the log collection layer from the analytical layer. Utilizing HTTPS encapsulation and cryptographic token authentication (SPLUNK_HEC_TOKEN) ensures that log telemetry cannot be sniffed or altered in transit between the (host/me) and the central repository.

* ## ### 🎯 The Strategic Value: Translating Code into Active Defense

To understand why I even did this pipeline other than it is more fun with live logs.

- **Traditional Logging (The Old Way):** This is like having a security camera that records to a VHS tape inside a drawer. If a thief breaks in, the footage exists, but nobody knows it's happening until the next morning when someone checks the tape. In the cyber world, this is how companies get breached without noticing for months.
- **Architecture (The SOC Way):** my custom C# agent acts as an automated security guard standing at the front door. The second someone tries to pick the lock (a failed SSH login), the guard doesn't just write it down in a messy notebook they instantly translate the threat into a structured, universal language (JSON) and flash a red warning light directly onto the master control screen (Splunk SIEM) in the security room.

- ## 🗂️ CHAPTER 3: The Engineering & Troubleshooting Journey

In the real world, setting up a data pipeline never goes perfectly on the first try and it was the same for me. Security systems have network blocks, permission restrictions, and token requirements. Documenting how things broke and how I fixed them shows exactly how I managed to get everything running.

### 🚨 Roadblock 1: SSH Connection Refused (Port 22 Closed)
* **The Symptom:**  When I tried to simulate an SSH attack to test my lab, the connection was instantly rejected. No logs were generated.
* **The Root Cause: The Linux SSH daemon (sshd) was turned off inside my Kali VM 🤦🏿‍♂️.
* **The Resolution: I ran sudo systemctl start ssh to turn the service on, open Port 22, and start generating login logs.
  <img width="514" height="60" alt="image" src="https://github.com/user-attachments/assets/9bb77890-fa7e-46fe-9ab3-d2c94a0bddee" />


### 🚨 Roadblock 2: Splunk Connection Refused (Port 8088 Closed)
* **The Symptom:** The C# agent successfully caught local attacks but threw an error whenever it tried to forward them to Splunk.
 <img width="651" height="540" alt="image" src="https://github.com/user-attachments/assets/ab117fd3-d949-4e9f-baef-780abb8b11e3" />
* **The Root Cause:** The containerized Splunk instance (splunk-siem) had crashed in the background and was in an Exited (255) status.
* **The Resolution:** I jumped into the terminal and woke the container back up by running sudo docker start splunk-siem.

### 🚨 Roadblock 3: HTTP 403 Forbidden Error
* **The Symptom:** The network path was open, but Splunk actively blocked the data sent by the C# agent.
  
<img width="664" height="458" alt="image" src="https://github.com/user-attachments/assets/13df63a3-3dcf-451d-b607-c5087137aa8a" />

* **The Root Cause:** The C# script was using an old, hardcoded placeholder string instead of a real authentication key.
* **the Resolution:** I logged into the Splunk Web interface and generated a brand new cryptographic token, and passed it to the agent using the SPLUNK_HEC_TOKEN environment variable.

---

### 🟢 Pipeline Resolution: Successful Log Ingestion
Once the Splunk container finished its boot sequence and the correct HEC authentication token was applied, the entire telemetry pipeline stabilized perfectly.

<img width="620" height="531" alt="image" src="https://github.com/user-attachments/assets/385a11f2-5618-4c9d-b96d-0a45c48fa0d4" />

*   **The Result:** As shown above, the C# agent is now running flawlessly. It captures the raw SSH log line, parses it using Regex, packages it into JSON, and receives an HTTP 200 OK verification back from the SIEM: `[SIEM] 🟢 Payload successfully indexed in Splunk!`.

---

### ⚔️ Simulating the Attack (The Red Team Vector)
To stress test my pipeline and see how the dashboard handles an active threat, I simulated an automated attack loop. I wrote a quick Bash script to guess common usernames over the loopback interface
<img width="900" height="460" alt="image" src="https://github.com/user-attachments/assets/c185574a-7f59-4848-bd1c-bb194b8543f8" />

#### Metric 1: Verifying Ingested Logs & Fields
*  **Analyst Breakdown:** All I needed to type in the Splunk search bar was index="main". Because the C# agent sends the data as a clean JSON payload, Splunk automatically parses the log line.
*   It extracts key fields like target_user and source_ip on its own. This allows me to instantly see who is attacking the system and which account they are targeting without writing long, complex queries.

<img width="1060" height="728" alt="image" src="https://github.com/user-attachments/assets/c3829c04-2177-46e1-b061-521f32cbaf06" />


**Metric 2: Tracking Attack Speed (Time-Chart Panel)**

<img width="510" height="558" alt="image" src="https://github.com/user-attachments/assets/b98a037c-ea35-41fe-a95c-51c40fedbd75" />


- **Analyst Breakdown:** This query tells Splunk to count all incoming logs and group them into 1-minute blocks (`span=1m`). By tracking the speed of the events, I can can instantly spot the difference between a real human user who just forgot their password, and an automated script that is spamming the server with thousands of guesses a minute. It makes spotting brute force attacks easier .

## 🧠 Threat Intelligence & Analyst Mindset

### Understanding Authentication Abuse
In this lab, the attacker tried to blend into standard system behavior by executing standard SSH login attempts. As an analyst, detecting these activities requires looking for behavioral anomalies like high velocity connection attempts or a single local IP guessing random account names over and over rather than relying on malware signatures or file hashes.

### The Attack Mechanics (What we are hunting for)
When an automated brute force attack is launched against a Linux machine, it triggers a very predictable sequence of events inside the operating system:
*   **Socket Binding:** The attacker initiates an external connection on Port 22.
*   **Authentication Check:** The Linux Pluggable Authentication Modules (PAM) subsystem validates the credentials against local user accounts.
*   **Log Generation:** If the password or username is wrong, Linux writes an explicit failure entry directly to the system logs (`sshd: Failed password for invalid user...`).

By knowing this fundamental system behavior, we can program our C# agent to target these exact text patterns, transforming raw system logs into instant security alerts before an attacker can guess a password right.

---

## 🔮 Modern Threat Context: Why a Simple Lab Remains Vital in the AI Era
A common question today is whether analyzing a standard Linux brute force attack is still relevant when modern threat actors use automated AI playbooks. The short answer is yes. While AI can change the speed and scale of an attack, the underlying operating system mechanics and post compromise goals remain exactly the same.

### 1. Authentication Brute-Forcing is Still a Top Threat Vector
Threat actors do not always rely on sophisticated custom malware; instead, they target exposed administrative services to gain initial entry into a corporate network.
*   **The Evidence:** According to global threat reports from vendors like Fortinet and Trellix, public facing services like SSH and RDP remain the top entry vectors utilized by Advanced Persistent Threats (APTs) to breach perimeters.
*   **The Trend:** Security infrastructure research highlights a continuous surge in automated scanning and brute forcing, noting how automated botnets continuously spray internet facing ports to find weak credentials or unmonitored baseline servers.

### 2. The Core Telemetry Has Not Changed (Linux Fundamentals)
Whether an authentication attack is executed by a human hacker or an automated, AI-driven script, the destination operating system must still process the login according to Linux architectural rules.
*   **The Analytical Takeaway:** Modern AI-powered SIEM tools require human baseline tuning. An analyst who does not understand fundamental core system architecture and logging engines cannot effectively write detection rules, validate automated alerts, or hunt for hidden anomalies when a pipeline breaks.

---

## 🛑 Mitigation & Recommendations

To protect an enterprise network from this type of authentication abuse based on my findings, I recommend implementing the following three primary controls:

*   **Enforce Key-Based Authentication:** Disable password authentication entirely inside the `/etc/ssh/sshd_config` file and enforce the use of secure SSH cryptographic public/private keys.
*   **Implement Connection Rate-Limiting:** Deploy tools like `Fail2ban` or host-based firewall rules to automatically block source IP addresses after a set number of failed login attempts.
*   **Separate Access Control:** Ensure administrative interfaces are never exposed directly to the public internet; restrict SSH access so it can only be reached through an enterprise VPN or a secure bastion host.
*  (**CIA-Triad**)
  
*Of course, there is always more you can do to harden a system like changing the default SSH port or implementing Multi-Factor Authentication (MFA) and so on*

---

## ⚠️⚠️⚠️⚠️ Lab Scope vs. Real-World Enterprise Realism

I want to be completely honest here: in a production corporate network, threat hunting and security engineering are never this simple. In this lab, my dataset was perfectly clean, so typing `index="main"` or running a basic `timechart` made the attack stand out instantly. In the real world, it doesn't work that way.

### The Reality of an Enterprise Network:
*   **The Problem with Noise:** In a real corporate network, a SIEM receives millions of events every single minute from firewalls, Active Directories, and legacy applications. A raw brute force attack would completely drown in all that massive background noise.
*   **The Danger of False Positives:** A fast spike in a 1-minute timechart doesn't automatically mean a hacker is targeting the company. It could easily be an automated IT backup script using an expired password, or a group of employees coming back from vacation who all forgot their passwords at the same time.
*   **Proactive vs. Reactive Defense:** Real world defense cannot rely solely on waiting for a SIEM alert to pop up. A strong corporate security posture requires proactive **Vulnerability Management** using enterprise tools like Tenable or Qualys to discover and fix open ports or weak credential policies before an adversary even finds them.
*   **The GRC Framework Alignment:** Technical threat hunting is only half the battle. True enterprise resilience requires aligning incident response with **GRC (Governance, Risk, and Compliance)** frameworks. Understanding corporate risk appetite, asset criticality, compliance regulations, and strict Incident Response Playbooks is what transforms a technical alert into an effective corporate defense strategy.

### Why Building this Lab Was Necessary for me:
Even though these basic queries don't match the heavy math or complex correlation rules used in a enterprise grade Security Operations Center (SOC), **doing this project was vital and fun for my training.** 

You cannot deploy advanced, machine learning detection rules if you do not understand the absolute physical basics first. This project allowed me to roll up my sleeves and learn exactly how a Linux OS process formats an authentication error, how JSON parameters look when traveling over a secure HTTPS pipeline, and how Splunk maps out time variables. 


---

## 🏁 Final Verdict
*   **Investigation completed.** Threat verified as **True Positive** (Automated SSH Brute Force).
*   **MITRE ATT&CK techniques mapped**, and documentation updated in README.
*   **Lab environment has been fully purged** and testing logs cleared. Closing task.



