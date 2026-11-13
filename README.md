Lost in a Good Book 📚

Lost in a Good Book est une application web de gestion de bibliothèque développée avec ASP.NET Core MVC et Oracle Database.

Fonctionnalités

Gestion des utilisateurs : authentification et gestion des rôles (Administrateur, Bibliothécaire, Adhérent).

Gestion des livres : ajout, modification, suppression, recherche et consultation.

Gestion des emprunts : emprunts, retours, disponibilité et pénalités de retard.

Gestion des adhérents : consultation et modification des profils.

Statistiques : tableaux de bord et suivi des activités.

BookBot : assistant conversationnel utilisant une IA locale avec Ollama et Qwen 2.5 (3B).

Technologies utilisées

C# et ASP.NET Core MVC

Oracle Database 11g XE

ADO.NET et Oracle.ManagedDataAccess

HTML, CSS, JavaScript et Bootstrap

Razor Views

Ollama et Qwen 2.5 (3B)

Architecture

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

Installation

Cloner le dépôt :

git clone https://github.com/ayetnouri/Bibliotheque.git

Ouvrir Bibliotheque.slnx avec Visual Studio.

Configurer la connexion Oracle dans la configuration locale.

Vérifier que la base Oracle est disponible.

Pour BookBot, installer Ollama et télécharger le modèle :

ollama pull qwen2.5:3b

Lancer le projet depuis Visual Studio.

Auteur

Ayet Nouri
Étudiante en 3ème année – Business Information System
École Supérieure de l'Économie Numérique (ESEN)
