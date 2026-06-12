# PCL-N Privacy Policy

**Last Updated: June 11, 2026**
**Effective Date: June 11, 2026**
**Version: v2.0**

## 1. Introduction & Definitions
Welcome to PCL-N. This Privacy Policy applies to the PCL-N client software and its accompanying cloud services (collectively, the "Service").
* **"We", "Us", or "Developer"** refers to MuXue1230-owo, the independent developer of PCL-N.
* **"Personal Data"** means any information relating to an identified or identifiable natural person.
* **"Device"** refers to the hardware on which you run the PCL-N client.

We are committed to protecting your privacy. This policy explains what data we collect, how we use it, your rights, and how we handle cross-border data transfers.

## 2. Data We Collect
We adhere to the principle of data minimization. We only collect information strictly necessary to operate the Service.

### 2.1 Data You Provide
* **Authentication Tokens:** When you sign in with a Microsoft account, we obtain OAuth 2.0 Access and Refresh Tokens via the official authorization flow. **We never collect, store, or process your plaintext passwords.**
* **User-Generated Content (UGC):** Local offline skin images uploaded via the "Wardrobe" feature and text messages sent through the "Friends Chat" system.
* **Sync Configuration:** JSON-formatted launcher settings (UI themes, memory allocation, download preferences) that you explicitly choose to sync to the cloud.

### 2.2 Automatically Collected Data
* **Network Identifiers (Sensitive):** When using the "P2P Multiplayer Lobby", our signaling server collects your **Public IP Address (IPv4/IPv6) and UDP/TCP ports**. This is strictly required to establish peer-to-peer connections.
* **Device & Environment Telemetry:** OS version, CPU architecture, Java Runtime Environment (JRE) path/version, and available RAM. This data is processed locally for environment adaptation and is only uploaded if you voluntarily submit a crash report.
* **Local Storage & Cookies:** OAuth tokens are encrypted locally using Windows DPAPI. The embedded WebView2 control uses local cookies to maintain Microsoft login sessions. These cookies never leave your device.

## 3. How We Use Your Data
* **Service Provision:** Routing P2P signaling data, validating game ownership via Microsoft/Mojang APIs, and syncing your configuration across devices.
* **Security & Integrity:** Verifying client authenticity via mTLS certificate fingerprints and JWT tokens to prevent API abuse and unauthorized access.
* **Communication:** Delivering chat messages and presence status between you and your authorized friends.

## 4. Data Sharing & Disclosure
We **do not sell, rent, or trade** your Personal Data to any third party. Data is only shared in the following circumstances:
* **P2P Peers:** Your Public IP and port are directly exchanged with the specific player you choose to connect with in the P2P Lobby.
* **Official Service Providers:** Your OAuth tokens and Minecraft UUID are transmitted directly to Microsoft/Mojang servers for license verification.
* **Legal Compliance:** We may disclose data if required by a valid subpoena, court order, or governmental regulation from a jurisdiction with competent authority.

## 5. Cross-Border Data Transfers
Our cloud infrastructure and databases are hosted in **Mainland China**. If you access the Service from outside Mainland China (including the EU, UK, US, or other regions), your data will be transferred to, stored, and processed in China.
* **Legal Basis:** By using the Service, you explicitly consent to this cross-border transfer. The transfer is necessary for the performance of the contract between you and the Developer.
* **Data Protection Standards:** We implement industry-standard technical safeguards (TLS 1.3, mTLS, AES-256 encryption at rest) to ensure your data receives a level of protection consistent with international standards, regardless of jurisdiction.

## 6. Data Security & Retention
* **Transmission:** All client-server communication is encrypted via TLS 1.2/1.3 with mandatory mTLS client authentication.
* **Retention Periods:**
  * **Chat Messages:** Cached for operational delivery and automatically purged after 7 days.
  * **IP Addresses:** Held only in volatile server memory during active P2P sessions. Never written to persistent logs.
  * **Inactive Accounts:** Cloud sync data associated with accounts inactive for 365 consecutive days may be anonymized or permanently deleted to conserve resources.

## 7. Your Rights & Controls
Depending on your jurisdiction (including GDPR, CCPA/CPRA, and PIPL), you may have the right to access, rectify, export, or delete your Personal Data.
* **Exercise Your Rights:** You may view synced data in the client settings. To delete all cloud data, use the "Clear Cloud Data" or "Terminate Account" function in the client. This action is irreversible.
* **Response Time:** We will respond to verifiable data requests within 30 days.

## 8. Children's Privacy
The Service is not intended for children under the age of 13 (or 14 in jurisdictions such as Mainland China). We do not knowingly collect Personal Data from children. If we become aware that a child has provided data without verifiable parental consent, we will promptly delete it.

## 9. Changes to This Policy
We may update this policy to reflect technical or legal changes. Material changes will be communicated via an in-app notice or GitHub repository announcement. Continued use constitutes acceptance of the revised policy.
