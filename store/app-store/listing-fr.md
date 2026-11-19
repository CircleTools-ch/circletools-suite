# App Store listing — Français (fr-FR)

App Store Connect → *Secret Santa* → ajouter la langue **Français**. La Suisse romande, la Belgique et
le Luxembourg utilisent la même localisation.

> **Owed:** a native-speaker read of the French copy ([backlog PR-7](../../docs/roadmap/backlog.md)).

## Nom (≤30)

```
Secret Santa – Père Noël
```

_From the next iOS version (backlog ASO-2, [ADR-0057, amendment 2026-11-17](../../docs/adr/0057-app-store-naming-and-localization.md)):
the brand first, then the search term. Until then "Secret Santa - CircleTools". With "Secret" already in the
brand, all three words of « père noël secret » are in the name; "Secret Santa: Père Noël secret" (the Play
title) is taken. The name must be unique on the store; if App Store Connect refuses it, use
`Secret Santa – Tirage au sort`._

## Sous-titre (≤30)

```
Tirage au sort, sans compte
```

_From the next iOS version (ASO-2). Was « Père Noël secret, sans compte »; « Père Noël » is now in the name,
so « tirage au sort » takes its place here._

## Texte promotionnel (≤170)

```
Noël approche ! Tirez les noms en quelques secondes, envoyez un lien privé à chacun et gardez le secret. Hors ligne, sans compte, gratuit et sans publicité.
```

## Mots-clés (≤100)

```
échange de cadeaux,liste de souhaits,pige,famille,bureau,collègues,fête,amis,cadeau,anonyme
```

_From the next iOS version (ASO-2): « Noël » is now in the name and « tirage au sort » in the subtitle, so
they leave this field; new: collègues, cadeau, anonyme. Was:
`tirage au sort,cadeaux,Noël,échange,liste de souhaits,famille,bureau,pige,fête,amis`._

## Description (≤4000)

_Revue pour la 1.0.14 (backlog PR-17) : même texte que la fiche Play. Saisie dans App Store Connect pour la version 1.0.14 le 2026-11-10._

```
Secret Santa est l'application simple pour organiser votre Père Noël secret – en famille, entre amis, entre collègues ou dans votre association. Tirez les noms au sort équitablement, envoyez à chacun un lien privé, et le secret reste un secret.

Personne n'a besoin d'installer quoi que ce soit ni de créer un compte. Vous organisez tout dans l'application – elle fonctionne même hors ligne – et les participants n'ont besoin que du lien que vous leur envoyez.

COMMENT ÇA MARCHE
• Créez un groupe et ajoutez les participants
• Planifiez l'occasion : date, lieu et budget optionnel
• Définissez qui ne peut pas tirer qui (exclusions)
• Tirez les noms d'un seul geste
• Partagez le lien privé de chaque personne par WhatsApp, Messages, e-mail ou code QR
• Chacun ouvre son lien pour voir à qui il offre, avec la liste de souhaits de cette personne

FONCTIONNALITÉS PRINCIPALES
• Tirage au sort équitable, avec exclusions pour les couples, les familles ou les attributions récurrentes
• Liens privés – chacun ne voit que la personne qu'il a tirée
• Listes de souhaits – chacun ajoute ses envies directement depuis son lien
• Notes cadeau pour les allergies ou ce qu'il ne faut pas offrir
• Budget optionnel, en fourchette ou simple maximum
• Rappels avant le grand jour, et ajout de la date à votre calendrier
• Mettez un groupe en ligne pour que chacun le rejoigne par un lien
• L'an prochain, recommencez avec le même groupe – sans répéter les paires de l'an passé
• Fonctionne hors ligne – aucun compte requis
• Gratuit, sans publicité et sans pistage
• Compte optionnel pour synchroniser entre téléphone, tablette et web, avec passkey ou mot de passe

IDÉAL POUR
• Noël en famille – toute la famille participe au tirage, même les proches qui habitent loin
• Le bureau et l'équipe – un échange de cadeaux sans tableur ni papiers dans un chapeau
• Les amis et les colocs – prêt en quelques minutes, les liens partent dans le chat du groupe
• Les associations, les classes et les grands groupes – des exclusions qui fonctionnent, tout simplement
• Toute l'année – un tirage au sort convient à tout échange de cadeaux, pas seulement en décembre

PRIVÉ PAR CONCEPTION
Sans compte, vos groupes restent sur votre appareil. Seul ce que vous choisissez de partager – un groupe mis en ligne, ou votre compte si vous en créez un – est enregistré, sur des serveurs en Suisse. Pas de publicité, pas de traceurs, pas de profilage. Exportez ou supprimez vos données à tout moment, directement dans l'application.

QUESTIONS FRÉQUENTES
Tout le monde a-t-il besoin de l'application Père Noël secret ?
Non. Seule la personne qui organise utilise l'application. Les autres ouvrent leur lien privé dans n'importe quel navigateur.

Faut-il un compte ?
Non. Sans compte, vos groupes restent sur votre téléphone. Un compte n'est utile que pour synchroniser entre plusieurs appareils.

Est-ce gratuit ?
Oui – sans publicité, sans achats intégrés et sans abonnement.

Peut-on recommencer l'an prochain ?
Oui. Créez un nouvel événement dans le même groupe en reprenant celui de l'an passé – le tirage évite alors les paires de l'an passé.

EN FRANÇAIS, ALLEMAND ET ANGLAIS
L'application suit la langue de votre appareil, et vous pouvez en changer à tout moment.

Que ce soit pour Noël, une fête de bureau ou une réunion de famille, Secret Santa enlève le tracas des échanges de cadeaux – pour que vous puissiez profiter du plaisir.
```

## Nouveautés (≤4000) — par mise à jour ; ici la version suivant la 1.0.14

```
Secret Santa est désormais open source : « À propos & mentions légales » affiche la licence et un lien vers le code source. Ainsi que des mises à jour de sécurité et de petites améliorations.
```

## TestFlight — Description de l'app bêta (≤4000)

```
Secret Santa organise un échange de cadeaux pour votre famille ou vos amis : créez un groupe, ajoutez tout le monde, définissez des exclusions, tirez les noms au sort et envoyez à chacun un lien privé qui révèle uniquement qui il a tiré, avec sa liste de souhaits.

L'application fonctionne entièrement hors ligne et sans compte. La connexion est facultative et ne sert qu'à synchroniser entre appareils ou à rejoindre des groupes via un compte.

Merci de tester ! Faites un tour complet – créer un groupe, tirer au sort, partager les liens, en ouvrir un – et signalez tout ce qui semble confus, mal affiché ou qui ne fonctionne pas.
```

## TestFlight — Éléments à tester (≤4000)

```
Merci de faire un tour complet :
1. Créez un groupe, ajoutez 4 à 6 personnes et une occasion (date, budget).
2. Ajoutez une exclusion (p. ex. un couple), puis tirez les noms au sort.
3. Partagez quelques liens privés (WhatsApp, Messages, copier, QR) et ouvrez-en un sur un autre téléphone.
4. Depuis un lien ouvert, ajoutez votre propre liste de souhaits.
5. Facultatif : créez un compte, connectez-vous sur un deuxième appareil et vérifiez que le groupe apparaît.
6. Facultatif : autorisez les rappels et vérifiez qu'une notification arrive.

Signalez ce qui est confus, les textes maladroits et les plantages (TestFlight → Envoyer un commentaire bêta, ou une capture d'écran).
```
