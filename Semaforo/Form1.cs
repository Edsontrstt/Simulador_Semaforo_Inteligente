#nullable disable

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Semaforo
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer timerSemaforo;
        private System.Windows.Forms.Timer timerReloj;

        private enum FaseLuz { Verde, Amarillo, Rojo }
        private FaseLuz faseActual = FaseLuz.Verde;

        private int tiempoRestante = 0;
        private int tiempoTotalFase = 0;
        private bool enEjecucion = false;

        // Tiempos: { Verde, Amarillo, Rojo } segun densidad
        private static readonly int[,] TIEMPOS = {
            { 15, 4, 45 }, // 0: Baja (Noche/Madrugada)
            { 30, 4, 30 }, // 1: Media (Día normal)
            { 50, 4, 15 }  // 2: Alta (Hora Punta)
        };

        // Controles UI
        private Panel pnlContenedor, pnlSemaforo;
        private Panel pnlRojo, pnlAmarillo, pnlVerde;
        private Label lblEstado, lblCuentaRegresiva;
        private Label lblTiempoVerde, lblTiempoAmarillo, lblTiempoRojo;
        private ComboBox cbDensidad;
        private CheckBox chkModoAuto;
        private Label lblAlertaReloj, lblHoraActual;
        private Button btnIniciar, btnDetener;
        private ProgressBar pbTiempo;
        private ListBox lbRegistro;

        public Form1()
        {
            InitializeComponent();
            InicializarTimers();
            ActualizarTiemposUI();
            this.Resize += (s, e) => CentrarContenedor();
        }

        private void CentrarContenedor()
        {
            if (pnlContenedor != null)
            {
                pnlContenedor.Left = (this.ClientSize.Width - pnlContenedor.Width) / 2;
                pnlContenedor.Top = (this.ClientSize.Height - pnlContenedor.Height) / 2;
            }
        }

        private void InicializarTimers()
        {
            // Timer del Semáforo
            timerSemaforo = new System.Windows.Forms.Timer { Interval = 1000 };
            timerSemaforo.Tick += (s, e) => {
                tiempoRestante--;
                ActualizarCuentaRegresiva();
                if (tiempoRestante <= 0) CambiarFase();
            };

            // Timer del Reloj del Sistema
            timerReloj = new System.Windows.Forms.Timer { Interval = 1000 };
            timerReloj.Tick += (s, e) => VerificarHoraSistema();
            timerReloj.Start();
        }

        private void VerificarHoraSistema()
        {
            lblHoraActual.Text = DateTime.Now.ToString("HH:mm:ss");

            if (!chkModoAuto.Checked) return;

            int hora = DateTime.Now.Hour;
            int nuevaDensidad = 1; // Media
            string mensaje = "";
            Color colorAlerta = Color.Gray;

            // Hora punta: 7am-9am y 5pm-8pm (17 a 20 hrs)
            if ((hora >= 7 && hora <= 9) || (hora >= 17 && hora <= 20))
            {
                nuevaDensidad = 2; // Alta
                mensaje = "⚠️ MODO HORA PUNTA: Desfogue activo";
                colorAlerta = Color.FromArgb(200, 40, 40);
            }
            // Madrugada: 11pm a 5am
            else if (hora >= 23 || hora <= 5)
            {
                nuevaDensidad = 0; // Baja
                mensaje = "🌙 MODO NOCTURNO: Flujo reducido";
                colorAlerta = Color.FromArgb(40, 100, 200);
            }
            // Resto del día
            else
            {
                nuevaDensidad = 1; // Media
                mensaje = "✅ TRÁFICO FLUIDO: Ciclo estándar";
                colorAlerta = Color.FromArgb(30, 160, 30);
            }

            lblAlertaReloj.Text = mensaje;
            lblAlertaReloj.ForeColor = colorAlerta;

            if (cbDensidad.SelectedIndex != nuevaDensidad)
            {
                cbDensidad.SelectedIndex = nuevaDensidad;
            }
        }

        private void chkModoAuto_CheckedChanged(object sender, EventArgs e)
        {
            cbDensidad.Enabled = !chkModoAuto.Checked;
            if (chkModoAuto.Checked)
            {
                RegistrarCambio("Modo Automático Activado");
                VerificarHoraSistema();
            }
            else
            {
                lblAlertaReloj.Text = "Modo Manual (Seleccione densidad)";
                lblAlertaReloj.ForeColor = Color.Gray;
                RegistrarCambio("Modo Manual Activado");
            }
        }

        private void CambiarFase()
        {
            faseActual = faseActual switch
            {
                FaseLuz.Verde => FaseLuz.Amarillo,
                FaseLuz.Amarillo => FaseLuz.Rojo,
                _ => FaseLuz.Verde
            };

            ConfigurarTiemposFase();
            ActualizarVisuales();
            RegistrarCambio();
        }

        private void ConfigurarTiemposFase()
        {
            int col = faseActual == FaseLuz.Verde ? 0 : (faseActual == FaseLuz.Amarillo ? 1 : 2);
            tiempoTotalFase = TIEMPOS[cbDensidad.SelectedIndex, col];
            tiempoRestante = tiempoTotalFase;
        }

        private void btnIniciar_Click(object sender, EventArgs e)
        {
            timerSemaforo.Stop();
            faseActual = FaseLuz.Verde;
            ConfigurarTiemposFase();
            enEjecucion = true;

            ActualizarVisuales();
            ActualizarTiemposUI();
            RegistrarCambio("Ciclo de semáforo iniciado");

            timerSemaforo.Start();
            btnIniciar.Enabled = false;
            btnDetener.Enabled = true;
        }

        private void btnDetener_Click(object sender, EventArgs e)
        {
            timerSemaforo.Stop();
            enEjecucion = false;
            ApagarLuces();

            lblEstado.Text = "Detenido";
            lblCuentaRegresiva.Text = "--";
            pbTiempo.Value = 0;

            RegistrarCambio("Ciclo de semáforo interrumpido");
            btnIniciar.Enabled = true;
            btnDetener.Enabled = false;
        }

        private void cbDensidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActualizarTiemposUI();
            if (enEjecucion)
            {
                RegistrarCambio("Ajuste de tiempos aplicado");
                btnIniciar_Click(null, EventArgs.Empty);
            }
        }

        private void ActualizarVisuales()
        {
            ApagarLuces();
            switch (faseActual)
            {
                case FaseLuz.Rojo:
                    pnlRojo.BackColor = Color.FromArgb(255, 60, 60);
                    lblEstado.Text = "🔴 ROJO - Detener";
                    lblEstado.ForeColor = Color.FromArgb(255, 80, 80);
                    pbTiempo.ForeColor = Color.FromArgb(220, 50, 50);
                    break;
                case FaseLuz.Amarillo:
                    pnlAmarillo.BackColor = Color.FromArgb(255, 215, 0);
                    lblEstado.Text = "🟡 AMARILLO - Precaución";
                    lblEstado.ForeColor = Color.FromArgb(200, 160, 0);
                    pbTiempo.ForeColor = Color.FromArgb(215, 180, 0);
                    break;
                case FaseLuz.Verde:
                    pnlVerde.BackColor = Color.FromArgb(46, 204, 64);
                    lblEstado.Text = "🟢 VERDE - Avanzar";
                    lblEstado.ForeColor = Color.FromArgb(46, 180, 46);
                    pbTiempo.ForeColor = Color.FromArgb(30, 180, 30);
                    break;
            }
            ActualizarCuentaRegresiva();
        }

        private void ApagarLuces()
        {
            pnlRojo.BackColor = Color.FromArgb(50, 0, 0);
            pnlAmarillo.BackColor = Color.FromArgb(42, 34, 0);
            pnlVerde.BackColor = Color.FromArgb(0, 50, 0);
        }

        private void ActualizarCuentaRegresiva()
        {
            lblCuentaRegresiva.Text = $"{tiempoRestante}s";
            if (tiempoTotalFase > 0)
            {
                pbTiempo.Value = (int)((tiempoRestante / (float)tiempoTotalFase) * 100);
            }
        }

        private void ActualizarTiemposUI()
        {
            int d = cbDensidad.SelectedIndex;
            lblTiempoVerde.Text = $"{TIEMPOS[d, 0]}s";
            lblTiempoAmarillo.Text = $"{TIEMPOS[d, 1]}s";
            lblTiempoRojo.Text = $"{TIEMPOS[d, 2]}s";
        }

        private void RegistrarCambio(string eventoPersonalizado = "")
        {
            string log = string.IsNullOrEmpty(eventoPersonalizado)
                ? $"[{DateTime.Now:HH:mm:ss}] Fase: {faseActual} | Flujo: {cbDensidad.SelectedItem.ToString().Split(' ')[0]}"
                : $"[{DateTime.Now:HH:mm:ss}] {eventoPersonalizado}";

            lbRegistro.Items.Insert(0, log);
            if (lbRegistro.Items.Count > 100) lbRegistro.Items.RemoveAt(lbRegistro.Items.Count - 1);
        }

        private void InitializeComponent()
        {
            this.Text = "Controlador de Tráfico Inteligente";
            this.Size = new Size(760, 660);
            this.MinimumSize = new Size(760, 660);
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.StartPosition = FormStartPosition.CenterScreen;

            pnlContenedor = new Panel { Size = new Size(720, 620) };

            pnlSemaforo = new Panel { Location = new Point(30, 30), Size = new Size(130, 380), BackColor = Color.FromArgb(25, 25, 25) };
            pnlSemaforo.Paint += (s, e) => {
                using var p = new Pen(Color.FromArgb(60, 60, 60), 2);
                e.Graphics.DrawRoundedRectangle(p, 1, 1, pnlSemaforo.Width - 2, pnlSemaforo.Height - 2, 20);
            };

            pnlRojo = CrearLuz(new Point(28, 25));
            pnlAmarillo = CrearLuz(new Point(28, 130));
            pnlVerde = CrearLuz(new Point(28, 235));
            pnlSemaforo.Controls.AddRange(new Control[] { pnlRojo, pnlAmarillo, pnlVerde });

            lblEstado = new Label { Location = new Point(30, 425), Size = new Size(130, 30), TextAlign = ContentAlignment.MiddleCenter, Text = "Detenido", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.Gray };
            lblCuentaRegresiva = new Label { Location = new Point(30, 460), Size = new Size(130, 50), TextAlign = ContentAlignment.MiddleCenter, Text = "--", Font = new Font("Segoe UI", 28f, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60) };
            pbTiempo = new ProgressBar { Location = new Point(30, 518), Size = new Size(130, 14), Style = ProgressBarStyle.Continuous };

            int rx = 200;

            // CORRECCIONES AUTOSIZE AQUÍ ABAJO PARA EVITAR QUE SE CORTE EL TEXTO
            var lblTitulo = new Label { Location = new Point(rx, 30), AutoSize = true, Text = "Dashboard de Control Vehicular", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 40, 40) };

            lblHoraActual = new Label { Location = new Point(540, 33), Size = new Size(120, 25), Text = "00:00:00", Font = new Font("Consolas", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60), TextAlign = ContentAlignment.MiddleRight };

            chkModoAuto = new CheckBox { Location = new Point(rx, 65), AutoSize = true, Text = "Modo Automático (Basado en reloj del sistema)", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 40, 40), Checked = true };
            chkModoAuto.CheckedChanged += chkModoAuto_CheckedChanged;

            cbDensidad = new ComboBox { Location = new Point(rx, 95), Size = new Size(220, 26), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10f), Enabled = false };
            cbDensidad.Items.AddRange(new[] { "Baja (Prioridad principal)", "Media (Flujo estándar)", "Alta (Desfogue rápido)" });
            cbDensidad.SelectedIndex = 1;
            cbDensidad.SelectedIndexChanged += cbDensidad_SelectedIndexChanged;

            lblAlertaReloj = new Label { Location = new Point(rx, 125), AutoSize = true, Text = "Evaluando hora...", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

            var pnlTiempos = new Panel { Location = new Point(rx, 155), Size = new Size(460, 90), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            CrearLabel(pnlTiempos, "Verde", 10, 8, true);
            CrearLabel(pnlTiempos, "Amarillo", 150, 8, true);
            CrearLabel(pnlTiempos, "Rojo", 290, 8, true);
            lblTiempoVerde = CrearLabel(pnlTiempos, "--", 10, 40, false, Color.FromArgb(30, 160, 30));
            lblTiempoAmarillo = CrearLabel(pnlTiempos, "--", 150, 40, false, Color.FromArgb(180, 140, 0));
            lblTiempoRojo = CrearLabel(pnlTiempos, "--", 290, 40, false, Color.FromArgb(200, 40, 40));

            btnIniciar = new Button { Location = new Point(rx, 260), Size = new Size(110, 36), Text = "Iniciar Ciclo", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(210, 240, 210), ForeColor = Color.FromArgb(30, 120, 30), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnIniciar.FlatAppearance.BorderColor = Color.FromArgb(120, 200, 120);
            btnIniciar.Click += btnIniciar_Click;

            btnDetener = new Button { Location = new Point(rx + 120, 260), Size = new Size(110, 36), Text = "Interrumpir", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245, 220, 220), ForeColor = Color.FromArgb(160, 30, 30), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Enabled = false };
            btnDetener.FlatAppearance.BorderColor = Color.FromArgb(200, 120, 120);
            btnDetener.Click += btnDetener_Click;

            var lblRegTit = new Label { Location = new Point(rx, 310), Size = new Size(200, 20), Text = "Log de auditoría:", ForeColor = Color.FromArgb(80, 80, 80) };
            lbRegistro = new ListBox { Location = new Point(rx, 330), Size = new Size(460, 185), Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle };

            pnlContenedor.Controls.AddRange(new Control[] { pnlSemaforo, lblEstado, lblCuentaRegresiva, pbTiempo, lblTitulo, lblHoraActual, chkModoAuto, cbDensidad, lblAlertaReloj, pnlTiempos, btnIniciar, btnDetener, lblRegTit, lbRegistro });
            this.Controls.Add(pnlContenedor);
            CentrarContenedor();
            ApagarLuces();

            VerificarHoraSistema();
        }

        private Panel CrearLuz(Point loc) => new Panel { Location = loc, Size = new Size(74, 74) };

        private Label CrearLabel(Control parent, string txt, int x, int y, bool isTitle, Color? c = null)
        {
            var l = new Label
            {
                Location = new Point(x, y),
                Size = new Size(135, isTitle ? 22 : 38),
                Text = txt,
                Font = new Font("Segoe UI", isTitle ? 9f : 20f, FontStyle.Bold),
                ForeColor = c ?? Color.Black
            };
            parent.Controls.Add(l);
            return l;
        }
    }

    public static class GraphicsExtensions
    {
        public static void DrawRoundedRectangle(this Graphics g, Pen pen, float x, float y, float w, float h, float r)
        {
            using var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}