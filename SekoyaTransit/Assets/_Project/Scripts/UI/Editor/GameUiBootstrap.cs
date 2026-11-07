#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SekoyaTransit.UI.Shell;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Editor
{
    /// <summary>
    /// Creates production Localization, PanelSettings, and wires the MapPlay shell (not the WebGL spike).
    /// </summary>
    public static class GameUiBootstrap
    {
        private const string LocDir = "Assets/_Project/Localization";
        private const string UiDir = "Assets/_Project/UI";
        private const string GeneratedDir = UiDir + "/Generated";
        private const string PanelPath = GeneratedDir + "/MapPlayPanelSettings.asset";
        private const string ThemePath = UiDir + "/Theme/GameTheme.tss";
        private const string UxmlPath = UiDir + "/Shell/MapPlayShell.uxml";
        private const string ScenePath = "Assets/_Project/Scenes/MapPlay.unity";
        private const string LocalizationSettingsPath = LocDir + "/Localization Settings.asset";
        private const string CargoIconsDir = IconSvgImportSettings.IconsRoot + "Cargo";
        private const string CargoFallbackIconPath = IconSvgImportSettings.IconsRoot + "Common/cargo_generic.svg";
        private const char NoBreakSpace = ' ';

        /// <summary>
        /// Generated cargo icon set wired into the MapPlay shell (Issue #182).
        /// </summary>
        public const string CargoIconSetPath = GeneratedDir + "/CargoIcons.asset";

        // Every text of the GameUi table in English and French (Issue #338). The table holds exactly these keys: a key
        // removed here is removed from the table on the next run, so it never keeps dead strings. French texts are
        // written with plain spaces; FrenchTypography turns the spaces French typography keeps attached into
        // non-breaking spaces when the table is written.
        private static readonly (string Key, string En, string Fr)[] ShellEntries =
        {
            (GameUiKeys.Cash, "{0}", "{0}"),
            (GameUiKeys.CashTooltip, "Cash", "Trésorerie"),
            (GameUiKeys.PopulationTooltip, "Population", "Population"),
            (GameUiKeys.CalendarDate, "Date: {0}", "Date : {0}"),
            (GameUiKeys.SpeedPause, "Pause", "Pause"),
            (GameUiKeys.SpeedX1, "Normal speed (x1)", "Vitesse normale (x1)"),
            (GameUiKeys.SpeedX2, "Fast (x2)", "Rapide (x2)"),
            (GameUiKeys.SpeedX3, "Fastest (x3)", "Très rapide (x3)"),
            (GameUiKeys.GameOverTitle, "Game Over", "Partie terminée"),
            (GameUiKeys.GameOverMessage, "Your company has gone bankrupt.", "Votre entreprise a fait faillite."),
            (GameUiKeys.ToolSelect, "Select (Esc)", "Sélection (Échap)"),
            (GameUiKeys.ToolRoad, "Road (B)", "Route (B)"),
            (GameUiKeys.ToolRail, "Rail (R)", "Rail (R)"),
            (GameUiKeys.ToolStation, "Road station (N)", "Gare routière (N)"),
            (GameUiKeys.ToolRailStation, "Rail station (G)", "Gare ferroviaire (G)"),
            (GameUiKeys.ToolDemolish, "Delete (X)", "Démolir (X)"),
            (GameUiKeys.VehiclesToggle, "Vehicles (L)", "Véhicules (L)"),
            (GameUiKeys.UnlocksToggle, "Unlocks (U)", "Déblocages (U)"),
            (GameUiKeys.VehiclesTitle, "Vehicles", "Véhicules"),
            (GameUiKeys.UnlocksTitle, "Vehicles unlock", "Déblocage des véhicules"),
            (GameUiKeys.UnlocksClose, "Close", "Fermer"),
            (GameUiKeys.UnlocksColumnTrucks, "Trucks", "Camions"),
            (GameUiKeys.UnlocksColumnTrains, "Trains", "Trains"),
            (GameUiKeys.UnlocksColumnBoats, "Boats", "Bateaux"),
            (GameUiKeys.UnlocksColumnPlanes, "Planes", "Avions"),
            (GameUiKeys.UnlocksCardUnlocked, "{0} - Unlocked {1}", "{0} - Débloqué en {1}"),
            (GameUiKeys.UnlocksCardUnlocks, "{0} - Unlocks {1}", "{0} - Déblocage en {1}"),
            (GameUiKeys.UnlocksStatCapacity, "Cargo capacity: {0}", "Capacité : {0}"),
            (GameUiKeys.UnlocksStatWagonCapacity, "Wagon capacity: {0}", "Capacité par wagon : {0}"),
            (GameUiKeys.UnlocksStatSpeed, "Speed: {0} tiles/s", "Vitesse : {0} cases/s"),
            (GameUiKeys.UnlocksStatMaxWagons, "Max wagons: {0}", "Wagons max : {0}"),
            (GameUiKeys.UnlocksStatPrice, "Price: {0}", "Prix : {0}"),
            (GameUiKeys.UnlocksStatUpkeep, "Upkeep: {0}/month", "Entretien : {0}/mois"),
            (GameUiKeys.UnlocksStatUpkeepRange, "Upkeep: {0} to {1}/month", "Entretien : {0} à {1}/mois"),
            (GameUiKeys.UnlocksStatusOwned, "Already owned", "Déjà débloqué"),
            (GameUiKeys.UnlocksStatusFuture, "Not yet available", "Pas encore disponible"),
            (GameUiKeys.UnlocksComingSoon, "Coming soon", "Bientôt disponible"),
            (GameUiKeys.UnlocksAction, "Buy for {0}", "Acheter pour {0}"),
            (GameUiKeys.VehiclesSell, "Sell for {0}", "Vendre pour {0}"),
            (GameUiKeys.VehiclesSellTrain, "Sell train for {0}", "Vendre le train pour {0}"),
            (GameUiKeys.VehiclesAddWagon, "Buy wagon for {0}", "Acheter un wagon pour {0}"),
            (GameUiKeys.VehiclesSellWagon, "Sell wagon for {0}", "Vendre un wagon pour {0}"),
            (
                GameUiKeys.VehiclesEmpty,
                "No vehicles yet. Open Routes (O) to create a route and buy vehicles for it.",
                "Aucun véhicule pour l'instant. Ouvrez Lignes (O) pour créer une ligne et y acheter des véhicules."),
            (GameUiKeys.VehiclesRow, "{1} #{0} - {2}", "{1} n°{0} - {2}"),
            (GameUiKeys.VehiclesRowRail, "{1} #{0} - {2} · Wagons {3}/{4}", "{1} n°{0} - {2} · Wagons {3}/{4}"),
            (GameUiKeys.VehiclesCargo, "Cargo ({0}/{1}): {2}", "Chargement ({0}/{1}) : {2}"),
            (GameUiKeys.VehiclesCargoEmpty, "Empty", "Vide"),
            (
                GameUiKeys.VehiclesFinance,
                "Last month upkeep/income: {0}/{1}",
                "Entretien/recettes du mois dernier : {0}/{1}"),
            (GameUiKeys.VehiclesBenefit, "Last month benefit: {0}", "Bénéfice du mois dernier : {0}"),
            (
                GameUiKeys.VehiclesFinanceUnknown,
                "Last month upkeep/income: Not available yet",
                "Entretien/recettes du mois dernier : pas encore disponible"),
            (GameUiKeys.VehiclesKindRoad, "Truck", "Camion"),
            (GameUiKeys.VehiclesKindRail, "Train", "Train"),
            (GameUiKeys.VehiclesStatusParked, "Parked", "À l'arrêt"),
            (GameUiKeys.VehiclesStatusMoving, "Moving", "En route"),
            (GameUiKeys.VehiclesStatusLoading, "Loading", "Chargement"),
            (GameUiKeys.VehiclesStatusStuck, "Stuck", "Bloqué"),
            (RoutesUiKeys.Toggle, "Routes (O)", "Lignes (O)"),
            (RoutesUiKeys.Title, "Routes", "Lignes"),
            (RoutesUiKeys.Close, "Close", "Fermer"),
            (RoutesUiKeys.ColumnName, "Route", "Ligne"),
            (RoutesUiKeys.ColumnRunningCost, "Running cost\n(last month)", "Coût d'exploitation\n(mois dernier)"),
            (RoutesUiKeys.ColumnRevenue, "Revenue\n(last month)", "Recettes\n(mois dernier)"),
            (RoutesUiKeys.ColumnProfit, "Profit\n(last month)", "Bénéfice\n(mois dernier)"),
            (RoutesUiKeys.ColumnStations, "Stations", "Gares"),
            (RoutesUiKeys.ColumnVehicles, "Vehicles", "Véhicules"),
            (
                RoutesUiKeys.Empty,
                "No routes yet. Create one, add at least two stations, then add vehicles.",
                "Aucune ligne pour l'instant. Créez-en une, ajoutez au moins deux gares, puis des véhicules."),
            (RoutesUiKeys.NoValue, "—", "—"),
            (RoutesUiKeys.Rename, "Edit", "Modifier"),
            (RoutesUiKeys.Delete, "Delete", "Supprimer"),
            (RoutesUiKeys.EditStations, "Add/Remove ({0})", "Ajouter/Retirer ({0})"),
            (RoutesUiKeys.EditVehicles, "Add/Remove ({0})", "Ajouter/Retirer ({0})"),
            (
                RoutesUiKeys.NeedsFix,
                "Needs at least 2 stations: add stations to run vehicles.",
                "Il faut au moins 2 gares : ajoutez des gares pour faire rouler des véhicules."),
            (RoutesUiKeys.Create, "Create a new route", "Créer une ligne"),
            (RoutesUiKeys.NameCreateTitle, "New route name", "Nom de la nouvelle ligne"),
            (RoutesUiKeys.NameRenameTitle, "Rename route", "Renommer la ligne"),
            (RoutesUiKeys.NameDefault, "Route {0}", "Ligne {0}"),
            (RoutesUiKeys.NameOk, "OK", "OK"),
            (RoutesUiKeys.NameCancel, "Cancel", "Annuler"),
            (RoutesUiKeys.NameErrorEmpty, "Enter a name.", "Entrez un nom."),
            (RoutesUiKeys.NameErrorTooLong, "Use at most 32 characters.", "32 caractères au maximum."),
            (RoutesUiKeys.NameErrorTaken, "Another route already has this name.", "Une autre ligne porte déjà ce nom."),
            (
                RoutesUiKeys.DeleteConfirm,
                "Delete route \"{0}\"? Its vehicles become unassigned and lose their cargo.",
                "Supprimer la ligne « {0} » ? Ses véhicules ne seront plus affectés et perdront leur chargement."),
            (RoutesUiKeys.DeleteYes, "Delete", "Supprimer"),
            (RoutesUiKeys.ConfirmNo, "Cancel", "Annuler"),
            (RoutesUiKeys.StationsHeader, "Stations:", "Gares :"),
            (RoutesUiKeys.StationNameRoad, "Road station #{0}", "Gare routière n°{0}"),
            (RoutesUiKeys.StationNameRail, "Rail station #{0}", "Gare ferroviaire n°{0}"),
            (RoutesUiKeys.StationsUp, "▲", "▲"),
            (RoutesUiKeys.StationsDown, "▼", "▼"),
            (RoutesUiKeys.StationsRemove, "Remove", "Retirer"),
            (RoutesUiKeys.StationsWaitForFull, "Wait for full", "Attendre le plein"),
            (
                RoutesUiKeys.StationsHint,
                "Click a station on the map to add it to this route.",
                "Cliquez sur une gare de la carte pour l'ajouter à cette ligne."),
            (
                RoutesUiKeys.StationsToolsLocked,
                "Construction is disabled while this window is open.",
                "La construction est désactivée tant que cette fenêtre est ouverte."),
            (RoutesUiKeys.StationsEmpty, "No stations yet.", "Aucune gare pour l'instant."),
            (
                RoutesUiKeys.StationsModeMismatch,
                "You cannot mix road and train stations!",
                "Impossible de mélanger gares routières et gares ferroviaires !"),
            (
                RoutesUiKeys.StationsRepeated,
                "This station is already the previous stop.",
                "Cette gare est déjà l'arrêt précédent."),
            (
                RoutesUiKeys.StationsRemoveConfirm,
                "With fewer than 2 stations, this route's vehicles become unassigned and lose their cargo. Remove anyway?",
                "Avec moins de 2 gares, les véhicules de cette ligne ne seront plus affectés et perdront leur chargement. Retirer quand même ?"),
            (RoutesUiKeys.StationsRemoveYes, "Remove", "Retirer"),
            (RoutesUiKeys.VehiclesHeader, "Vehicles:", "Véhicules :"),
            (RoutesUiKeys.VehiclesRemove, "Remove", "Retirer"),
            (RoutesUiKeys.VehiclesAddButton, "Add a vehicle", "Ajouter un véhicule"),
            (RoutesUiKeys.VehiclesAvailable, "Available vehicles", "Véhicules disponibles"),
            (RoutesUiKeys.VehiclesAdd, "Add", "Ajouter"),
            (RoutesUiKeys.VehiclesBuy, "Buy ({0})", "Acheter ({0})"),
            (RoutesUiKeys.VehiclesWagons, "Wagons", "Wagons"),
            (RoutesUiKeys.VehiclesEmpty, "No vehicles on this route.", "Aucun véhicule sur cette ligne."),
            (RoutesUiKeys.VehiclesNoneAvailable, "No unassigned vehicles.", "Aucun véhicule non affecté."),
            (
                RoutesUiKeys.VehiclesInvalidRoute,
                "Add at least 2 stations before adding vehicles.",
                "Ajoutez au moins 2 gares avant d'ajouter des véhicules."),
            (RoutesUiKeys.VehicleLabel, "{0} #{1}", "{0} n°{1}"),
            (
                RoutesUiKeys.VehiclesStationHint,
                "Vehicles of the routes that stop at this station.",
                "Véhicules des lignes qui s'arrêtent à cette gare."),
            (RoutesUiKeys.VehiclesRouteNone, "Route: None", "Ligne : aucune"),
            (RoutesUiKeys.VehiclesUnassignedHeader, "Unassigned vehicles", "Véhicules non affectés"),
            (RoutesUiKeys.VehiclesRoute, "Route:", "Ligne :"),
            (RoutesUiKeys.VehiclesStatusUnassigned, "Unassigned", "Non affecté"),
            (GameUiKeys.IndustriesToggle, "Industries (I)", "Industries (I)"),
            (GameUiKeys.IndustriesTitle, "Industries", "Industries"),
            (GameUiKeys.IndustriesEmpty, "No industries on this map.", "Aucune industrie sur cette carte."),
            (GameUiKeys.IndustriesSummary, "{0} #{1}", "{0} n°{1}"),
            (
                GameUiKeys.IndustriesSummaryGrowth,
                "{0} #{1} - Growth level {2}/{3}",
                "{0} n°{1} - Niveau de croissance {2}/{3}"),
            (GameUiKeys.IndustriesStock, "Current stock/max stock: {0}/{1} {2}", "Stock actuel/max : {0}/{1} {2}"),
            (GameUiKeys.IndustriesProduction, "Last month production: {0} {1}", "Production du mois dernier : {0} {1}"),
            (GameUiKeys.IndustriesDelivered, "Last month delivered: {0} {1}", "Livré le mois dernier : {0} {1}"),
            (
                GameUiKeys.IndustriesProgress,
                "Next level progress: {0}/{1} months",
                "Progression vers le niveau suivant : {0}/{1} mois"),
            (
                GameUiKeys.IndustriesProgressMax,
                "Next level progress: max level reached",
                "Progression vers le niveau suivant : niveau max atteint"),
            (GameUiKeys.IndustriesRulesToggle, "How to grow industries", "Faire grandir les industries"),
            (
                GameUiKeys.IndustriesRulesIntro,
                "Industries that produce raw goods grow a level when at least {0}% of their production is delivered for {1} months in a row (a month below that restarts the count). Each level multiplies their production and stock by x{2}, up to level {3}.",
                "Les industries de matières premières gagnent un niveau quand au moins {0} % de leur production est livrée pendant {1} mois d'affilée (un mois en dessous remet le compte à zéro). Chaque niveau multiplie leur production et leur stock par x{2}, jusqu'au niveau {3}."),
            (GameUiKeys.TownsToggle, "Towns (T)", "Villes (T)"),
            (GameUiKeys.TownsTitle, "Towns", "Villes"),
            (GameUiKeys.TownsEmpty, "No towns on this map.", "Aucune ville sur cette carte."),
            (GameUiKeys.TownsSummary, "{0} · {1} (+{2} last month)", "{0} · {1} (+{2} le mois dernier)"),
            (GameUiKeys.TownsNeedLine, "{0} {1}/month ({2} supplied last month)", "{0} {1}/mois ({2} livrés le mois dernier)"),
            (GameUiKeys.TownsPassengerLine, "Generated {0} · Transported {1}", "Générés {0} · Transportés {1}"),
            (GameUiKeys.TownsMailLine, "Generated {0} · Transported {1}", "Généré {0} · Transporté {1}"),
            (GameUiKeys.TownsRulesToggle, "How towns grow", "Croissance des villes"),
            (
                GameUiKeys.TownsRulesIntro,
                "Growing a town unlocks more needs and higher need levels, and makes it generate more passengers and mail.",
                "Une ville qui grandit débloque plus de besoins et des besoins de niveau supérieur, et génère plus de passagers et de courrier."),
            (
                GameUiKeys.TownsRulesPartlyMet,
                "Partly met need (at least {4}% of the demand) = {0} / {1} / {2} {3} growth each month (Raw / Processed / Finished)",
                "Besoin partiellement satisfait (au moins {4} % de la demande) = {0} / {1} / {2} {3} de croissance par mois (brut / transformé / fini)"),
            (
                GameUiKeys.TownsRulesFullyMet,
                "Fully met need = {0} / {1} / {2} {3} growth each month (Raw / Processed / Finished)",
                "Besoin pleinement satisfait = {0} / {1} / {2} {3} de croissance par mois (brut / transformé / fini)"),
            (
                GameUiKeys.TownsRulesAllMet,
                "All needs fully met = x{0} {1} growth from all needs",
                "Tous les besoins pleinement satisfaits = x{0} {1} de croissance pour tous les besoins"),
            (GameUiKeys.TownsGrowthLevel, "Growth level: {0}/{1}, next at {2}", "Niveau de croissance : {0}/{1}, suivant à {2}"),
            (GameUiKeys.TownsGrowthLevelMax, "Growth level: {0}/{1} (max)", "Niveau de croissance : {0}/{1} (max)"),
            (GameUiKeys.TownsNeedsRaw, "Raw needs:", "Besoins bruts :"),
            (GameUiKeys.TownsNeedsProcessed, "Processed needs:", "Besoins transformés :"),
            (GameUiKeys.TownsNeedsFinished, "Finished needs:", "Besoins finis :"),
            (GameUiKeys.TownsNeedsNotAvailable, "Not available yet", "Pas encore disponible"),
            (GameUiKeys.IndustryForest, "Forest", "Forêt"),
            (GameUiKeys.IndustrySawmill, "Sawmill", "Scierie"),
            (GameUiKeys.IndustryFurnitureFactory, "Furniture factory", "Fabrique de meubles"),
            (GameUiKeys.IndustryWheatFarm, "Wheat farm", "Ferme céréalière"),
            (GameUiKeys.IndustryFlourMill, "Flour mill", "Minoterie"),
            (GameUiKeys.IndustryBakery, "Bakery", "Boulangerie"),
            (GameUiKeys.IndustryIronMine, "Iron mine", "Mine de fer"),
            (GameUiKeys.IndustryIronFoundry, "Iron foundry", "Fonderie"),
            (GameUiKeys.IndustryMachineShop, "Machine shop", "Atelier mécanique"),
            (GameUiKeys.IndustryLivestockRanch, "Livestock ranch", "Élevage de bétail"),
            (GameUiKeys.IndustryButcher, "Butcher", "Boucherie"),
            (GameUiKeys.IndustryCannery, "Cannery", "Conserverie"),
            (GameUiKeys.IndustrySheepFarm, "Sheep farm", "Bergerie"),
            (GameUiKeys.IndustryTextileMill, "Textile mill", "Filature"),
            (GameUiKeys.IndustryClothingWorkshop, "Clothing workshop", "Atelier de confection"),
            (GameUiKeys.VehicleTruckMk1, "Truck Mk1", "Camion Mk1"),
            (GameUiKeys.VehicleTruckMk2, "Truck Mk2", "Camion Mk2"),
            (GameUiKeys.VehicleTruckMk3, "Truck Mk3", "Camion Mk3"),
            (GameUiKeys.VehicleTruckMk4, "Truck Mk4", "Camion Mk4"),
            (GameUiKeys.VehicleTruckMk5, "Truck Mk5", "Camion Mk5"),
            (GameUiKeys.VehicleLocoMk1, "Loco Mk1", "Loco Mk1"),
            (GameUiKeys.VehicleLocoMk2, "Loco Mk2", "Loco Mk2"),
            (GameUiKeys.VehicleLocoMk3, "Loco Mk3", "Loco Mk3"),
            (GameUiKeys.VehicleLocoMk4, "Loco Mk4", "Loco Mk4"),
            (GameUiKeys.VehicleLocoMk5, "Loco Mk5", "Loco Mk5"),
            (GameUiKeys.VehicleRailWagon, "Rail wagon", "Wagon"),
            (GameUiKeys.CargoNamePrefix + "passengers", "Passengers", "Passagers"),
            (GameUiKeys.CargoNamePrefix + "mail", "Mail", "Courrier"),
            (GameUiKeys.CargoNamePrefix + "wood", "Wood", "Bois"),
            (GameUiKeys.CargoNamePrefix + "planks", "Planks", "Planches"),
            (GameUiKeys.CargoNamePrefix + "furniture", "Furniture", "Meubles"),
            (GameUiKeys.CargoNamePrefix + "wheat", "Wheat", "Blé"),
            (GameUiKeys.CargoNamePrefix + "flour", "Flour", "Farine"),
            (GameUiKeys.CargoNamePrefix + "bread", "Bread", "Pain"),
            (GameUiKeys.CargoNamePrefix + "iron_ore", "Iron ore", "Minerai de fer"),
            (GameUiKeys.CargoNamePrefix + "iron", "Iron", "Fer"),
            (GameUiKeys.CargoNamePrefix + "machinery", "Machinery", "Machines"),
            (GameUiKeys.CargoNamePrefix + "livestock", "Livestock", "Bétail"),
            (GameUiKeys.CargoNamePrefix + "meat", "Meat", "Viande"),
            (GameUiKeys.CargoNamePrefix + "canned_food", "Canned food", "Conserves"),
            (GameUiKeys.CargoNamePrefix + "wool", "Wool", "Laine"),
            (GameUiKeys.CargoNamePrefix + "fabric", "Fabric", "Tissu"),
            (GameUiKeys.CargoNamePrefix + "clothing", "Clothing", "Vêtements"),
            (GoalsUiKeys.FinanceToggle, "Finance (F)", "Finances (F)"),
            (GoalsUiKeys.FinanceTitle, "Finance", "Finances"),
            (GoalsUiKeys.FinanceColumnLastMonth, "Last month", "Mois dernier"),
            (GoalsUiKeys.FinanceColumnCurrentMonth, "Current month", "Mois en cours"),
            (GoalsUiKeys.FinanceColumnLastYear, "Last year", "Année dernière"),
            (GoalsUiKeys.FinanceColumnCurrentYear, "Current year", "Année en cours"),
            (GoalsUiKeys.FinanceNotAvailable, "Not available yet", "Pas encore disponible"),
            (GoalsUiKeys.FinanceTruckCost, "Truck vehicles maintenance cost", "Coût d'entretien des camions"),
            (GoalsUiKeys.FinanceTruckBenefit, "Truck vehicles benefit", "Recettes des camions"),
            (GoalsUiKeys.FinanceTruckProfit, "Truck vehicles profit", "Bénéfice des camions"),
            (GoalsUiKeys.FinanceTrainCost, "Train vehicles maintenance cost", "Coût d'entretien des trains"),
            (GoalsUiKeys.FinanceTrainBenefit, "Train vehicles benefit", "Recettes des trains"),
            (GoalsUiKeys.FinanceTrainProfit, "Train vehicles profit", "Bénéfice des trains"),
            (GoalsUiKeys.FinanceTotalCost, "Total vehicles maintenance cost", "Coût d'entretien total des véhicules"),
            (GoalsUiKeys.FinanceTotalBenefit, "Total vehicles benefit", "Recettes totales des véhicules"),
            (GoalsUiKeys.FinanceTotalVehiclesProfit, "Total vehicles profit", "Bénéfice total des véhicules"),
            (GoalsUiKeys.FinanceTruckBought, "Truck vehicles bought", "Camions achetés"),
            (GoalsUiKeys.FinanceTrainBought, "Train vehicles bought", "Trains achetés"),
            (GoalsUiKeys.FinanceVehiclesSold, "Vehicles sold", "Véhicules vendus"),
            (GoalsUiKeys.FinanceVehiclesUnlocked, "Vehicles unlocked", "Véhicules débloqués"),
            (GoalsUiKeys.FinanceContractProfit, "Contract profit", "Bénéfice des contrats"),
            (GoalsUiKeys.FinanceCheats, "Cheats", "Triches"),
            (GoalsUiKeys.FinanceTotalProfit, "Total profit", "Bénéfice total"),
            (GoalsUiKeys.AchievementsToggle, "Achievements (H)", "Succès (H)"),
            (GoalsUiKeys.AchievementsTitle, "Achievements", "Succès"),
            (GoalsUiKeys.AchievementsEmpty, "No achievements defined.", "Aucun succès défini."),
            (GoalsUiKeys.AchievementsClose, "Close (Esc)", "Fermer (Échap)"),
            (GoalsUiKeys.AchievementsSectionGlobal, "Achievements", "Succès"),
            (GoalsUiKeys.AchievementsSectionCompany, "Company goals", "Objectifs de l'entreprise"),
            (GoalsUiKeys.AchievementsSectionResources, "Resources delivered", "Ressources livrées"),
            (GoalsUiKeys.AchievementsTierBronze, "Bronze", "Bronze"),
            (GoalsUiKeys.AchievementsTierSilver, "Silver", "Argent"),
            (GoalsUiKeys.AchievementsTierGold, "Gold", "Or"),
            (GoalsUiKeys.ContractsToggle, "Contracts (K)", "Contrats (K)"),
            (GoalsUiKeys.ContractsTitle, "Contracts", "Contrats"),
            (GoalsUiKeys.ContractsAvailable, "Available: {0}/{1}", "Disponibles : {0}/{1}"),
            (GoalsUiKeys.ContractsInProgress, "In progress: {0}/{1}", "En cours : {0}/{1}"),
            (GoalsUiKeys.ContractsEmpty, "No contracts yet.", "Aucun contrat pour l'instant."),
            (GoalsUiKeys.ContractsRowDeliver, "Deliver {0} {1} to {2} in {3} months", "Livrer {0} {1} à {2} en {3} mois"),
            (GoalsUiKeys.ContractsRowReward, "Reward: {0}", "Récompense : {0}"),
            (GoalsUiKeys.ContractsRowAccepted, "Accepted: {0} / {1} until {2}", "Accepté : {0} / {1} jusqu'au {2}"),
            (GoalsUiKeys.ContractsRowCompleted, "Completed: {0} / {1} at {2}", "Rempli : {0} / {1} le {2}"),
            (GoalsUiKeys.ContractsRowFailed, "Failed: {0} / {1} at {2}", "Échoué : {0} / {1} le {2}"),
            (GoalsUiKeys.ContractsAccept, "Accept", "Accepter"),
            (GoalsUiKeys.ContractsDecline, "Decline", "Refuser"),
            (GoalsUiKeys.ContractsCancel, "Cancel", "Annuler"),
            (GoalsUiKeys.ContractsClaim, "Reward {0}", "Réclamer {0}"),
            (GoalsUiKeys.ContractsRemove, "Remove", "Retirer"),
            (GoalsUiKeys.ContractsFooterNext, "New contract will appear next month", "Un nouveau contrat apparaîtra le mois prochain"),
            (GoalsUiKeys.ContractsFooterFull, "The contract list is full", "La liste des contrats est pleine"),
            (GoalsUiKeys.ActiveContractsTitle, "Active contracts:", "Contrats actifs :"),
            (GoalsUiKeys.ActiveContractsDeliver, "Deliver {0} to {1}", "Livrer {0} à {1}"),
            (GoalsUiKeys.ActiveContractsProgress, "Progress: {0}/{1}", "Progression : {0}/{1}"),
            (GoalsUiKeys.ActiveContractsTimeLeft, "Time left: {0} days", "Temps restant : {0} jours"),
            (GoalsUiKeys.ActiveContractsTimeLeftOne, "Time left: {0} day", "Temps restant : {0} jour"),
            (
                GoalsUiKeys.ActiveContractsCompleted,
                "Completed! Click to claim Reward",
                "Rempli ! Cliquez pour réclamer la récompense"),
            (GoalsUiKeys.ToastAchievement, "{0} achievement unlocked: {1}", "Succès {0} débloqué : {1}"),
            (GoalsUiKeys.ToastContractOffered, "New contract: deliver {1} {0} to {2}", "Nouveau contrat : livrer {1} {0} à {2}"),
            (
                GoalsUiKeys.ToastContractCompleted,
                "Contract completed: {1} {0} to {2}. Claim the reward in the contracts panel",
                "Contrat rempli : {1} {0} à {2}. Réclamez la récompense dans le panneau des contrats"),
            (GoalsUiKeys.ToastContractFailed, "Contract failed: {1} {0} to {2}", "Contrat échoué : {1} {0} à {2}"),
            (GoalsUiKeys.GameOverScore, "Final score: {0}", "Score final : {0}"),
            (GoalsUiKeys.GameOverDate, "Bankruptcy date: {0}", "Date de la faillite : {0}"),
            (NewGameUiKeys.Title, "New game", "Nouvelle partie"),
            (NewGameUiKeys.SeedLabel, "Seed", "Graine"),
            (NewGameUiKeys.Random, "Random", "Aléatoire"),
            (
                NewGameUiKeys.SeedError,
                "Enter a whole number from 0 to 18446744073709551615.",
                "Entrez un nombre entier de 0 à 18446744073709551615."),
            (NewGameUiKeys.SizeLabel, "Map size", "Taille de la carte"),
            (NewGameUiKeys.SizeSmall, "Small 128×128", "Petite 128×128"),
            (NewGameUiKeys.SizeMedium, "Medium 256×256", "Moyenne 256×256"),
            (NewGameUiKeys.SizeLarge, "Large 512×512", "Grande 512×512"),
            (NewGameUiKeys.Start, "Start", "Commencer"),
            (NewGameUiKeys.SeedInfo, "Seed {0} · {1}×{1}", "Graine {0} · {1}×{1}"),
            (NewGameUiKeys.GameOverNewGame, "New game", "Nouvelle partie"),
            // Each language is named in its own language in every table, so a player can find their language
            // whatever language is shown.
            (ShellLanguages.EnglishNameKey, "English", "English"),
            (ShellLanguages.FrenchNameKey, "Français", "Français"),
            (SaveLoadUiKeys.Toggle, "Save / Load", "Sauvegarder / Charger"),
            (SaveLoadUiKeys.Title, "Save / Load", "Sauvegarder / Charger"),
            (SaveLoadUiKeys.Close, "Close", "Fermer"),
            (SaveLoadUiKeys.SlotAutosave, "Autosave", "Sauvegarde auto"),
            (SaveLoadUiKeys.SlotManual, "Slot {0}", "Emplacement {0}"),
            (SaveLoadUiKeys.SlotInfo, "{0} · Seed {1} · {2}×{2}", "{0} · Graine {1} · {2}×{2}"),
            (SaveLoadUiKeys.SlotEmpty, "Empty", "Vide"),
            (SaveLoadUiKeys.SlotUnreadable, "Unreadable save", "Sauvegarde illisible"),
            (SaveLoadUiKeys.Save, "Save", "Sauvegarder"),
            (SaveLoadUiKeys.Load, "Load", "Charger"),
            (
                SaveLoadUiKeys.ConfirmText,
                "Unsaved progress will be lost. Load anyway?",
                "La progression non sauvegardée sera perdue. Charger quand même ?"),
            (SaveLoadUiKeys.ConfirmYes, "Load anyway", "Charger quand même"),
            (SaveLoadUiKeys.ConfirmNo, "Cancel", "Annuler"),
            (SaveLoadUiKeys.StatusSaved, "Game saved.", "Partie sauvegardée."),
            (SaveLoadUiKeys.StatusSaveFailed, "The game could not be saved.", "La partie n'a pas pu être sauvegardée."),
            (SaveLoadUiKeys.StatusLoadFailed, "The game could not be loaded.", "La partie n'a pas pu être chargée."),
            (
                SaveLoadUiKeys.StatusNotSupported,
                "This save is damaged or was made by a newer version of the game.",
                "Cette sauvegarde est endommagée ou provient d'une version plus récente du jeu."),
            (
                SaveLoadUiKeys.StatusTooOld,
                "This save was made by an older version of the game and can no longer be loaded.",
                "Cette sauvegarde provient d'une ancienne version du jeu et ne peut plus être chargée."),
            (
                SaveLoadUiKeys.StatusAutosaveFailed,
                "The autosave failed; the game continues.",
                "La sauvegarde automatique a échoué ; la partie continue."),
            (
                SaveLoadUiKeys.StatusFileFailed,
                "The file could not be opened. Press Load from file to try again.",
                "Le fichier n'a pas pu être ouvert. Appuyez sur Charger un fichier pour réessayer."),
            (SaveLoadUiKeys.StatusDownloadFailed, "The game could not be downloaded.", "La partie n'a pas pu être téléchargée."),
            (SaveLoadUiKeys.LoadFromFile, "Load from file", "Charger un fichier"),
            (SaveLoadUiKeys.DebugDownload, "Debug: download this game", "Debug : télécharger cette partie"),
            (SaveLoadUiKeys.Continue, "Continue", "Continuer"),
            (SaveLoadUiKeys.LoadGame, "Load game", "Charger une partie"),
            (SoundUiKeys.Toggle, "Sound", "Son"),
            (SoundUiKeys.Title, "Sound", "Son"),
            (SoundUiKeys.Music, "Music volume", "Volume de la musique"),
            (SoundUiKeys.Effects, "Effects volume", "Volume des effets"),
            (SoundUiKeys.Mute, "Mute all sound", "Couper tous les sons"),
            (SoundUiKeys.Close, "Close", "Fermer"),
            ("achievement.global", "Achievements unlocked", "Succès débloqués"),
            ("achievement.global.bronze", "Achiever", "Méritant"),
            ("achievement.global.silver", "Pro Player", "Joueur confirmé"),
            ("achievement.global.gold", "Completionist", "Perfectionniste"),
            ("achievement.lifetime_income", "Money earned", "Argent gagné"),
            ("achievement.lifetime_income.bronze", "Profit Surge", "Poussée de profits"),
            ("achievement.lifetime_income.silver", "Money Machine", "Machine à billets"),
            ("achievement.lifetime_income.gold", "Cash Empire", "Empire financier"),
            ("achievement.contracts", "Contracts fulfilled", "Contrats remplis"),
            ("achievement.contracts.bronze", "Reliable Partner", "Partenaire fiable"),
            ("achievement.contracts.silver", "Contract Ace", "As des contrats"),
            ("achievement.contracts.gold", "Deal Legend", "Légende des affaires"),
            ("achievement.vehicle_models", "Vehicle models unlocked", "Modèles de véhicules débloqués"),
            ("achievement.vehicle_models.bronze", "Early Adopter", "Pionnier"),
            ("achievement.vehicle_models.silver", "Fleet Modernizer", "Modernisateur de flotte"),
            ("achievement.vehicle_models.gold", "State of the Art", "À la pointe"),
            ("achievement.trucks", "Trucks owned", "Camions possédés"),
            ("achievement.trucks.bronze", "Truck Stop", "Relais routier"),
            ("achievement.trucks.silver", "Convoy Captain", "Chef de convoi"),
            ("achievement.trucks.gold", "Road King", "Roi de la route"),
            ("achievement.trains", "Trains owned", "Trains possédés"),
            ("achievement.trains.bronze", "Station Master", "Chef de gare"),
            ("achievement.trains.silver", "Rail Baron", "Baron du rail"),
            ("achievement.trains.gold", "Railway Tycoon", "Magnat du chemin de fer"),
            ("achievement.road_distance", "Road distance (tiles)", "Distance sur route (cases)"),
            ("achievement.road_distance.bronze", "Road Tripper", "Routard"),
            ("achievement.road_distance.silver", "Long Hauler", "Grand routier"),
            ("achievement.road_distance.gold", "Asphalt Legend", "Légende de l'asphalte"),
            ("achievement.rail_distance", "Rail distance (tiles)", "Distance sur rail (cases)"),
            ("achievement.rail_distance.bronze", "Rail Rookie", "Apprenti cheminot"),
            ("achievement.rail_distance.silver", "Iron Horse", "Cheval de fer"),
            ("achievement.rail_distance.gold", "Steel Meridian", "Méridien d'acier"),
            ("achievement.largest_town", "Largest town", "Plus grande ville"),
            ("achievement.largest_town.bronze", "Village Builder", "Bâtisseur de village"),
            ("achievement.largest_town.silver", "Town Maker", "Faiseur de villes"),
            ("achievement.largest_town.gold", "City Founder", "Fondateur de cité"),
            ("achievement.sold_passengers.bronze", "Shuttle Service", "Service de navette"),
            ("achievement.sold_passengers.silver", "People Mover", "Transporteur de foules"),
            ("achievement.sold_passengers.gold", "Mass Transit", "Transport de masse"),
            ("achievement.sold_mail.bronze", "Postman", "Facteur"),
            ("achievement.sold_mail.silver", "Express Courier", "Coursier express"),
            ("achievement.sold_mail.gold", "Postal Empire", "Empire postal"),
            ("achievement.sold_wood.bronze", "Log Haul", "Transport de grumes"),
            ("achievement.sold_wood.silver", "Timber Run", "Route du bois"),
            ("achievement.sold_wood.gold", "Wood Magnate", "Magnat du bois"),
            ("achievement.sold_planks.bronze", "Plank Walker", "Porteur de planches"),
            ("achievement.sold_planks.silver", "Lumber Line", "Ligne du bois d'œuvre"),
            ("achievement.sold_planks.gold", "Board Baron", "Baron des planches"),
            ("achievement.sold_furniture.bronze", "Chair Lift", "Porteur de chaises"),
            ("achievement.sold_furniture.silver", "Home Furnisher", "Ensemblier"),
            ("achievement.sold_furniture.gold", "Furniture Mogul", "Magnat du meuble"),
            ("achievement.sold_wheat.bronze", "Harvest Hand", "Moissonneur"),
            ("achievement.sold_wheat.silver", "Grain Train", "Train du grain"),
            ("achievement.sold_wheat.gold", "Wheat King", "Roi du blé"),
            ("achievement.sold_flour.bronze", "Flour Power", "Fleur de farine"),
            ("achievement.sold_flour.silver", "Mill Runner", "Navette du moulin"),
            ("achievement.sold_flour.gold", "Flour Tycoon", "Magnat de la farine"),
            ("achievement.sold_bread.bronze", "Daily Bread", "Pain quotidien"),
            ("achievement.sold_bread.silver", "Bread Winner", "Gagne-pain"),
            ("achievement.sold_bread.gold", "Upper Crust", "Haut du panier"),
            ("achievement.sold_iron_ore.bronze", "Ore Digger", "Mineur"),
            ("achievement.sold_iron_ore.silver", "Ore Express", "Express du minerai"),
            ("achievement.sold_iron_ore.gold", "Mountain Mover", "Déplaceur de montagnes"),
            ("achievement.sold_iron.bronze", "Iron Will", "Volonté de fer"),
            ("achievement.sold_iron.silver", "Smelter Supplier", "Fournisseur de fonderie"),
            ("achievement.sold_iron.gold", "Iron Magnate", "Magnat du fer"),
            ("achievement.sold_machinery.bronze", "Gear Shift", "Changement de vitesse"),
            ("achievement.sold_machinery.silver", "Machine Master", "Maître des machines"),
            ("achievement.sold_machinery.gold", "Industrial Titan", "Titan de l'industrie"),
            ("achievement.sold_livestock.bronze", "Cattle Drive", "Transhumance"),
            ("achievement.sold_livestock.silver", "Herd Hauler", "Convoyeur de troupeaux"),
            ("achievement.sold_livestock.gold", "Cattle Baron", "Baron du bétail"),
            ("achievement.sold_meat.bronze", "Butcher's Helper", "Commis boucher"),
            ("achievement.sold_meat.silver", "Meat Packer", "Conditionneur de viande"),
            ("achievement.sold_meat.gold", "Meat Mogul", "Magnat de la viande"),
            ("achievement.sold_canned_food.bronze", "Can Do", "Mise en boîte"),
            ("achievement.sold_canned_food.silver", "Pantry Filler", "Garde-manger plein"),
            ("achievement.sold_canned_food.gold", "Canning Empire", "Empire de la conserve"),
            ("achievement.sold_wool.bronze", "Shear Luck", "Coup de ciseaux"),
            ("achievement.sold_wool.silver", "Fleece Fleet", "Flotte de toisons"),
            ("achievement.sold_wool.gold", "Wool Baron", "Baron de la laine"),
            ("achievement.sold_fabric.bronze", "Thread Runner", "Fil conducteur"),
            ("achievement.sold_fabric.silver", "Cloth Carrier", "Porteur d'étoffes"),
            ("achievement.sold_fabric.gold", "Textile Titan", "Titan du textile"),
            ("achievement.sold_clothing.bronze", "Dressed Up", "Tiré à quatre épingles"),
            ("achievement.sold_clothing.silver", "Trend Setter", "Faiseur de tendances"),
            ("achievement.sold_clothing.gold", "Fashion Empire", "Empire de la mode"),
        };

        /// <summary>
        /// Gets every GameUi key with its English and French text as written to the tables (French typography
        /// applied).
        /// </summary>
        public static IReadOnlyList<(string Key, string En, string Fr)> Entries()
        {
            var entries = new List<(string Key, string En, string Fr)>(ShellEntries.Length);
            foreach ((string key, string en, string fr) in ShellEntries)
            {
                entries.Add((key, en, FrenchTypography(fr)));
            }

            return entries;
        }

        /// <summary>
        /// Replaces the plain space French typography keeps attached before <c>: ; ! ? %</c> and inside « » with a
        /// non-breaking space, so a line never breaks between a word and that sign.
        /// </summary>
        public static string FrenchTypography(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return text
                .Replace(" :", NoBreakSpace + ":")
                .Replace(" ;", NoBreakSpace + ";")
                .Replace(" !", NoBreakSpace + "!")
                .Replace(" ?", NoBreakSpace + "?")
                .Replace(" %", NoBreakSpace + "%")
                .Replace("« ", "«" + NoBreakSpace)
                .Replace(" »", NoBreakSpace + "»");
        }

        [MenuItem("Sekoya Transit/UI/Ensure Game UI Assets")]
        public static void EnsureFromMenu()
        {
            EnsureAll();
            Debug.Log("Game UI assets ensured (localization, panel, MapPlay shell wiring).");
        }

        /// <summary>
        /// Batchmode: Unity -batchmode -quit -executeMethod SekoyaTransit.UI.Editor.GameUiBootstrap.EnsureFromBatch
        /// </summary>
        public static void EnsureFromBatch()
        {
            EnsureAll();
        }

        public static void EnsureAll()
        {
            EnsureFolders();
            EnsureLocalization();
            EnsurePanelSettings();
            EnsureIconImports();
            EnsureCargoIconSet();
            EnsureMapPlayShellWired();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(LocDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(GeneratedDir.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureLocalization()
        {
            LocalizationSettings settings = LoadOrCreateLocalizationSettings();
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            Locale en = FindOrCreateLocale(ShellLanguages.English);
            Locale fr = FindOrCreateLocale(ShellLanguages.French);
            foreach (Locale locale in new[] { en, fr })
            {
                if (!LocalizationEditorSettings.GetLocales().Contains(locale))
                {
                    LocalizationEditorSettings.AddLocale(locale);
                }
            }

            // A French text that is ever missing shows in English instead of the "No translation found" text.
            if (fr.Metadata.GetMetadata<FallbackLocale>() == null)
            {
                fr.Metadata.AddMetadata(new FallbackLocale(en));
                EditorUtility.SetDirty(fr);
            }

            StringTableCollection? collection = LocalizationEditorSettings.GetStringTableCollection(
                GameUiKeys.Table);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    GameUiKeys.Table,
                    LocDir,
                    new List<Locale> { en, fr });
            }

            StringTable enTable = GetOrAddTable(collection, en);
            StringTable frTable = GetOrAddTable(collection, fr);
            var shippedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, string enText, string frText) in Entries())
            {
                if (!shippedKeys.Add(key))
                {
                    throw new InvalidDataException("GameUi key listed twice: " + key);
                }

                SetEntry(enTable, key, enText);
                SetEntry(frTable, key, frText);
            }

            foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries.ToArray())
            {
                if (!shippedKeys.Contains(entry.Key))
                {
                    collection.RemoveEntry(entry.Key);
                }
            }

            RemoveUnshippedLocales(en, fr);
            // A locale without a translation resolves through the fallback locale instead of the "No translation" text.
            settings.GetStringDatabase().UseFallback = true;
            EnsureStartupSelectors(settings, en);

            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(frTable);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(settings);
        }

        private static StringTable GetOrAddTable(StringTableCollection collection, Locale locale)
        {
            if (collection.GetTable(locale.Identifier) is StringTable existing)
            {
                return existing;
            }

            return (StringTable)collection.AddNewTable(locale.Identifier);
        }

        private static void SetEntry(StringTable table, string key, string text)
        {
            StringTableEntry? existing = table.GetEntry(key);
            if (existing == null)
            {
                table.AddEntry(key, text);
            }
            else
            {
                existing.Value = text;
            }
        }

        /// <summary>
        /// The startup language (Issue #338): a <c>-language=</c> command line argument (development), then the
        /// language the player chose in the start menu, then the system or browser language when it is a shipped one,
        /// then English.
        /// </summary>
        private static void EnsureStartupSelectors(LocalizationSettings settings, Locale en)
        {
            List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
            // Rebuilt only when it differs, so a run that changes nothing leaves the settings asset unchanged.
            if (selectors.Count == 4
                && selectors[0] is CommandLineLocaleSelector
                && selectors[1] is StoredLanguageLocaleSelector
                && selectors[2] is SystemLocaleSelector
                && selectors[3] is SpecificLocaleSelector specific
                && specific.LocaleId.Equals(en.Identifier))
            {
                return;
            }

            selectors.Clear();
            selectors.Add(new CommandLineLocaleSelector());
            selectors.Add(new StoredLanguageLocaleSelector());
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = en.Identifier });
        }

        /// <summary>
        /// English and French are the shipped locales (ADR 0011, Issue #338). Drops every other registered locale and
        /// its string tables, so the system locale selector cannot pick a locale without a GameUi table. The
        /// Localization package registers every Locale asset it imports, so the project must hold no other Locale
        /// asset either (Issue #330).
        /// </summary>
        private static void RemoveUnshippedLocales(Locale en, Locale fr)
        {
            foreach (StringTableCollection other in LocalizationEditorSettings.GetStringTableCollections())
            {
                foreach (StringTable table in other.StringTables.ToArray())
                {
                    if (table.LocaleIdentifier != en.Identifier && table.LocaleIdentifier != fr.Identifier)
                    {
                        other.RemoveTable(table);
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(table));
                    }
                }
            }

            foreach (Locale locale in LocalizationEditorSettings.GetLocales().ToArray())
            {
                if (locale != null && locale != en && locale != fr)
                {
                    LocalizationEditorSettings.RemoveLocale(locale);
                }
            }
        }

        private static LocalizationSettings LoadOrCreateLocalizationSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
            return settings;
        }

        private static Locale FindOrCreateLocale(string code)
        {
            foreach (Locale locale in LocalizationEditorSettings.GetLocales())
            {
                if (locale != null && locale.Identifier.Code == code)
                {
                    return locale;
                }
            }

            string localePath = LocDir + "/Locale-" + code + ".asset";
            var existingAsset = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
            if (existingAsset != null)
            {
                return existingAsset;
            }

            Locale created = Locale.CreateLocale(new LocaleIdentifier(code));
            AssetDatabase.CreateAsset(created, localePath);
            return created;
        }

        private static void EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            // Fonts and elements scale with the screen, so the layout looks the same at any resolution or browser zoom.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null)
            {
                panel.themeStyleSheet = theme;
            }

            EditorUtility.SetDirty(panel);
        }

        private static void EnsureIconImports()
        {
            string root = IconSvgImportSettings.IconsRoot.TrimEnd('/');
            foreach (string file in Directory.GetFiles(root, "*.svg", SearchOption.AllDirectories))
            {
                IconSvgImportSettings.EnsureVectorImage(file.Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        // One entry per Art/Icons/Cargo/<cargo id>.svg, sorted by id; the generic crate is the fallback.
        private static void EnsureCargoIconSet()
        {
            VectorImage fallback = AssetDatabase.LoadAssetAtPath<VectorImage>(CargoFallbackIconPath)
                ?? throw new FileNotFoundException("Generic cargo icon is missing.", CargoFallbackIconPath);
            string[] files = Directory.GetFiles(CargoIconsDir, "*.svg", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            var icons = new List<KeyValuePair<string, VectorImage>>(files.Length);
            foreach (string file in files)
            {
                string path = file.Replace(Path.DirectorySeparatorChar, '/');
                VectorImage icon = AssetDatabase.LoadAssetAtPath<VectorImage>(path)
                    ?? throw new InvalidDataException("Cargo icon did not import as a vector image: " + path);
                icons.Add(new KeyValuePair<string, VectorImage>(Path.GetFileNameWithoutExtension(path), icon));
            }

            var set = AssetDatabase.LoadAssetAtPath<CargoIconSet>(CargoIconSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CargoIconSet>();
                AssetDatabase.CreateAsset(set, CargoIconSetPath);
            }

            set.SetIcons(icons, fallback);
            EditorUtility.SetDirty(set);
        }

        private static void EnsureMapPlayShellWired()
        {
            if (!File.Exists(ScenePath.Replace('/', Path.DirectorySeparatorChar)))
            {
                throw new FileNotFoundException(
                    "MapPlay scene is missing; run MapPlaySceneBuilder first.",
                    ScenePath);
            }

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (panel == null || uxml == null)
            {
                throw new InvalidOperationException(
                    "MapPlay panel settings or MapPlayShell.uxml is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MapPlayShell? shell = null;
            UIDocument? document = null;
            GameObject? sessionObject = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root.GetComponent("MapPlaySession") != null)
                {
                    sessionObject = root;
                }

                shell ??= root.GetComponent<MapPlayShell>();
                document ??= root.GetComponent<UIDocument>();
            }

            if (sessionObject == null)
            {
                throw new InvalidDataException("MapPlay scene has no MapPlaySession component.");
            }

            if (shell == null)
            {
                shell = sessionObject.AddComponent<MapPlayShell>();
            }

            document = shell.GetComponent<UIDocument>();
            if (document == null)
            {
                document = sessionObject.AddComponent<UIDocument>();
            }

            document.panelSettings = panel;
            document.visualTreeAsset = uxml;
            var shellObject = new SerializedObject(shell);
            shellObject.FindProperty("_cargoIcons").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CargoIconSet>(CargoIconSetPath);
            shellObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sessionObject);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Builds Addressables for the active target (required before WebGL player builds).
        /// </summary>
        public static void BuildAddressablesForActiveTarget()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings are missing.");
            }

            AddressableAssetSettings.BuildPlayerContent();
        }
    }
}
