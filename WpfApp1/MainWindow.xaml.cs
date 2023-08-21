using Hardcodet.Wpf.TaskbarNotification;
using SocketIOClient;
using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using WpfApp1.Classes;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private SocketIO socket;
        private DispatcherTimer timer;
        private Questionnaire questionnaire;
        private int currentQuestionIndex;
        private int correctAnswers;
        private TaskbarIcon taskbarIcon;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            ConnectToServer(true);
            InitializeSocket();
            questionnaire = new Questionnaire();
            InitializeTaskbarIcon();
            HideToTray();
        }

        private void InitializeTaskbarIcon()
        {
            taskbarIcon = new TaskbarIcon
            {
                Icon = Properties.Resources.icon,
                ToolTipText = "Родительский контроль"
            };
            taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_DoubleClick;
        }

        private void TaskbarIcon_DoubleClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.Show();
            this.Activate();
        }

        private void HideToTray()
        {
            this.Hide();
        }

        private void ShowQuestion(int index)
        {
            if (index < questionnaire.Questions.Count)
            {
                Question question = questionnaire.Questions[index];
                textBlock.Text = question.Text;

                answerStackPanel.Children.Clear();

                StackPanel buttonStackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                foreach (string option in question.Options)
                {
                    Button button = new Button
                    {
                        Content = option,
                        Style = (Style)FindResource("ColoredButtonStyle"),
                        Tag = option
                    };
                    button.Click += AnswerButton_Click;
                    buttonStackPanel.Children.Add(button);
                }

                answerStackPanel.Children.Add(buttonStackPanel);
            }
            else
            {
                textBlock.Text = $"Тест завершен. Правильных ответов: {correctAnswers} из {questionnaire.Questions.Count}";
                answerStackPanel.Children.Clear();
            }
        }


        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HideToTray();
            }
            else if (WindowState == WindowState.Normal)
            {
                taskbarIcon.Visibility = Visibility.Collapsed;
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (timer != null && timer.IsEnabled)
            {
                e.Cancel = true;
                MessageBox.Show("Пожалуйста, дождитесь завершения таймера.");
            }
            else
            {
                e.Cancel = true;
                HideToTray();
            }
            base.OnClosing(e);
        }


        private void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Question question = questionnaire.Questions[currentQuestionIndex];

            if (question.Options.IndexOf(button.Tag.ToString()) == question.CorrectIndex)
            {
                correctAnswers++;
            }

            currentQuestionIndex++;
            ShowQuestion(currentQuestionIndex);
        }

        private async void ConnectToServer(bool connect)
        {
            using (HttpClient client = new HttpClient())
            {
                string action = connect ? "connect" : "disconnect";
                Uri url = new Uri($"http://localhost:3000?action={action}");
                await client.GetAsync(url);

                Console.WriteLine(connect ? "Connected" : "Disconnected");
            }
        }

        private void InitializeSocket()
        {
            socket = new SocketIO("http://localhost:3000");

            socket.On("time-received", (response) => {
                int timeInSeconds = response.GetValue<int>();
                Dispatcher.Invoke(() => {
                    StartTimer(timeInSeconds);
                });
            });
            socket.ConnectAsync();
        }

        private void StartTimer(int timeInSeconds)
        {
            if (timer != null && timer.IsEnabled)
            {
                timer.Stop();
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            int remainingTime = timeInSeconds;

            timer.Tick += (sender, e) => {
                UpdateTextBlock($"{TimeSpan.FromSeconds(remainingTime).ToString("hh':'mm':'ss")}");
                remainingTime--;

                if (remainingTime < 0)
                {
                    this.Show();
                    this.Activate();
                    timer.Stop();
                    UpdateTextBlock("Время вышло!");

                    currentQuestionIndex = 0;
                    correctAnswers = 0;
                    ShowQuestion(currentQuestionIndex);

                    WindowState = WindowState.Maximized;

                    Topmost = true;
                }
            };

            timer.Start();
        }

        private void UpdateTextBlock(string text)
        {
            textBlock.Text = text;
        }

        protected override void OnClosed(EventArgs e)
        {
            ConnectToServer(false);
            base.OnClosed(e);
        }

        public MainWindow(IntPtr hWnd) : this()
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = hWnd;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                Topmost = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (timer != null && timer.IsEnabled)
            {
                e.Cancel = true;
                MessageBox.Show("Пожалуйста, дождитесь завершения таймера.");
            }
        }
    }
}
