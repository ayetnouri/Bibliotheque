                                           Lost in a Good Book

Lost in a Good Book est une application web de gestion de bibliothèque développée avec ASP.NET Core MVC et Oracle Database.

✨ Fonctionnalités

Gestion des utilisateurs : authentification et gestion des rôles (Administrateur, Bibliothécaire et Adhérent).

Gestion des livres : ajout, modification, suppression, recherche et consultation.

Gestion des emprunts : emprunts, retours, disponibilité et pénalités de retard.

Gestion des adhérents : consultation et modification des profils.

Statistiques : tableaux de bord et suivi des activités.

BookBot : assistant conversationnel basé sur une IA locale avec Ollama et Qwen 2.5 (3B).

🛠️ Technologies utilisées

Domaine

Technologies

Backend

C#, ASP.NET Core MVC

Base de données

Oracle Database 11g XE

Accès aux données

ADO.NET, Oracle.ManagedDataAccess

Frontend

HTML, CSS, JavaScript, Bootstrap

Interface

Razor Views

Intelligence artificielle

Ollama, Qwen 2.5 (3B)

🏗️ Architecture du projet

Bibliotheque/
├── Controllers/
├── Data/
├── Models/
├── Views/
├── wwwroot/
├── Properties/
├── Program.cs
├── appsettings.json
└── Bibliotheque.csproj

⚙️ Installation

1. Cloner le dépôt

git clone https://github.com/ayetnouri/Bibliotheque.git

2. Ouvrir le projet

Ouvrir Bibliotheque.slnx avec Visual Studio.

3. Configurer Oracle

Configurer la chaîne de connexion Oracle dans la configuration locale de l'application et vérifier que la base de données est disponible.

4. Configurer BookBot

Installer Ollama et télécharger le modèle :

ollama pull qwen2.5:3b

5. Lancer l'application

Démarrer le projet depuis Visual Studio.

👩‍💻 Auteur

Ayet Nouri

Étudiante en business computing  – Business Information System
École Supérieure de l'Économie Numérique (ESEN)
